using System.IO;
using System.Text;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Orchestration;
using AutolumoBarcodeScannerTool.Core.Sinks;
using AutolumoBarcodeScannerTool.Core.Sources;
using AutolumoBarcodeScannerTool.Core.Sources.Serial;
using AutolumoBarcodeScannerTool.Core.Transforms;
using AutolumoBarcodeScannerTool.Hid;
using AutolumoBarcodeScannerTool.Tray;
using AutolumoBarcodeScannerTool.Win32;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace AutolumoBarcodeScannerTool;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutolumoBarcodeScannerTool");
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "appsettings.ini");
        if (!File.Exists(configPath))
        {
            var defaultIni = Path.Combine(AppContext.BaseDirectory, "appsettings.default.ini");
            if (File.Exists(defaultIni)) File.Copy(defaultIni, configPath);
        }

        var logsDir = Path.Combine(configDir, "logs");
        Directory.CreateDirectory(logsDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(logsDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Single-instance guard, scoped to the current user session. Prevents
        // multiple tray icons and double LL hooks when autostart already brought
        // one up at login and the user double-clicks the .exe again. We use
        // "Local\\" (the default session namespace) instead of "Global\\" because
        // the latter requires SeCreateGlobalPrivilege which standard users lack.
        using var singleInstance = new System.Threading.Mutex(
            initiallyOwned: true,
            name: @"Local\AutolumoBarcodeScannerTool_v1",
            out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "Autolumo Barcode Scanner Tool ya está en ejecución. Revisa el ícono en la bandeja del sistema.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var host = BuildHost(configPath, args);
            host.Start();

            // CRITICAL: start the orchestrator on the STA Main thread (not via
            // IHostedService which can run on the threadpool). The HID source
            // creates a NativeWindow and installs a WH_KEYBOARD_LL hook — both
            // require the calling thread to own the message pump that
            // Application.Run() is about to start.
            var orchestrator = host.Services.GetRequiredService<ScannerOrchestrator>();
            var scannerOpts = host.Services.GetRequiredService<IOptionsMonitor<ScannerOptions>>().CurrentValue;

            // Honor Enabled flag from config. If false, leave the orchestrator
            // stopped — the user can resume from the tray menu.
            if (scannerOpts.Enabled)
                orchestrator.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            var tray = host.Services.GetRequiredService<TrayIconController>();
            tray.Show(startEnabled: scannerOpts.Enabled);

            Application.Run(new ApplicationContext());

            orchestrator.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            host.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fallo fatal en arranque");
            MessageBox.Show(
                $"Error fatal: {ex.Message}\n\nRevisa los logs en %LOCALAPPDATA%\\AutolumoBarcodeScannerTool\\logs",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHost BuildHost(string configPath, string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration((_, builder) =>
            {
                builder.Sources.Clear();
                builder.AddIniFile(configPath, optional: false, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<ScannerOptions>(ctx.Configuration.GetSection(ScannerOptions.SectionName));
                services.Configure<TargetOptions>(ctx.Configuration.GetSection($"{ScannerOptions.SectionName}:Target"));
                services.Configure<AutostartOptions>(ctx.Configuration.GetSection(AutostartOptions.SectionName));
                services.Configure<LoggingOptions>(ctx.Configuration.GetSection(LoggingOptions.SectionName));

                services.AddSingleton(_ => configPath);
                services.AddSingleton<AutostartManager>();
                services.AddSingleton<IForegroundWindowProvider, Win32ForegroundWindowProvider>();
                services.AddSingleton<IInputInjector, SendInputInjector>();
                services.AddSingleton<ITerminatorTransform, ReplaceTerminatorTransform>();
                services.AddSingleton<IInputSink, ForegroundProcessSink>();

                services.AddSingleton<IInputSource>(sp =>
                {
                    var opts = sp.GetRequiredService<IOptionsMonitor<ScannerOptions>>().CurrentValue;
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    return opts.SourceType switch
                    {
                        SourceType.Serial => CreateSerial(opts, loggerFactory),
                        SourceType.HidKeyboard => CreateHid(opts, loggerFactory),
                        _ => throw new InvalidOperationException("SourceType desconocido")
                    };
                });

                services.AddSingleton<ScannerOrchestrator>();
                services.AddSingleton<TrayIconController>();
            })
            .Build();

    private static IInputSource CreateSerial(ScannerOptions opts, ILoggerFactory lf)
    {
        var encoding = opts.Serial.Encoding switch
        {
            SerialEncoding.Utf8 => Encoding.UTF8,
            SerialEncoding.Latin1 => Encoding.Latin1,
            _ => Encoding.ASCII
        };
        var adapter = new SystemSerialPortAdapter(new SerialPortConfig(
            opts.Serial.PortName, opts.Serial.BaudRate, opts.Serial.DataBits,
            opts.Serial.Parity, opts.Serial.StopBits, encoding));
        return new SerialInputSource(adapter, opts.Terminator, encoding,
            lf.CreateLogger<SerialInputSource>());
    }

    private static IInputSource CreateHid(ScannerOptions opts, ILoggerFactory lf) =>
        new HidKeyboardInputSource(
            opts.HidKeyboard.VendorId, opts.HidKeyboard.ProductId,
            opts.Terminator, lf.CreateLogger<HidKeyboardInputSource>());
}

