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

        try
        {
            using var host = BuildHost(configPath, args);
            host.Start();

            var tray = host.Services.GetRequiredService<TrayIconController>();
            tray.Show();

            Application.Run();

            host.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fallo fatal en arranque");
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
                services.Configure<OutputOptions>(ctx.Configuration.GetSection($"{ScannerOptions.SectionName}:Output"));
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
                services.AddHostedService<OrchestratorHostedService>();
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

internal sealed class OrchestratorHostedService : IHostedService
{
    private readonly ScannerOrchestrator _orchestrator;
    public OrchestratorHostedService(ScannerOrchestrator orchestrator) { _orchestrator = orchestrator; }
    public Task StartAsync(CancellationToken ct) => _orchestrator.StartAsync(ct);
    public Task StopAsync(CancellationToken ct) => _orchestrator.StopAsync(ct);
}
