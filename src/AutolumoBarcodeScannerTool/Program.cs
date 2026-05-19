using System.IO;
using System.Threading;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core;
using AutolumoBarcodeScannerTool.Diag;
using AutolumoBarcodeScannerTool.Hid;
using AutolumoBarcodeScannerTool.Tray;
using AutolumoBarcodeScannerTool.Win32;

namespace AutolumoBarcodeScannerTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Single-instance guard scoped to the current user session.
        using var mutex = new Mutex(initiallyOwned: true,
            name: @"Local\AutolumoBarcodeScannerTool_v1", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show(
                "Autolumo ya está en ejecución. Revisa la bandeja del sistema.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Config + logs live under %LOCALAPPDATA%\AutolumoBarcodeScannerTool\.
        var localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutolumoBarcodeScannerTool");
        var configDir = localData;
        var logDir = Path.Combine(localData, "logs");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "appsettings.ini");
        if (!File.Exists(configPath))
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "appsettings.default.ini");
            if (File.Exists(seedPath)) File.Copy(seedPath, configPath);
        }

        var config = ConfigIo.Load(configPath);
        AppLog.Init(logDir, config.VerboseLogging);
        AppLog.Info($"=== arranque v0.2.1 — VID={config.VendorId} PID={config.ProductId} " +
                    $"proc='{config.TargetProcessName}' title='{config.TargetWindowTitleContains}' " +
                    $"ignoreFilter={config.IgnoreWindowFilter} verbose={config.VerboseLogging} ===");

        var autostart = new AutostartManager();

        // Tracker registers a hidden Raw Input window for the configured VID/PID.
        // The window MUST be created on the STA Main thread that owns Application.Run().
        using var tracker = new ScannerKeyTracker(config.VendorId, config.ProductId);
        try
        {
            tracker.Start();
            AppLog.Info($"ScannerKeyTracker.Start OK — escuchando VID_{config.VendorId}&PID_{config.ProductId}");
        }
        catch (Exception ex)
        {
            AppLog.Info($"ScannerKeyTracker.Start FAILED: {ex.Message}");
            MessageBox.Show(
                $"No se pudo registrar el lector HID (VID={config.VendorId}, PID={config.ProductId}):\n\n{ex.Message}\n\n" +
                "Abre Configurar… desde la bandeja para seleccionar otro dispositivo.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // LL hook callback. Si IgnoreWindowFilter está activo, pasamos strings
        // vacíos como filtro: SpaceSuppressor ya trata "ambos vacíos" como
        // "suprime en cualquier app" (semántica probada).
        //
        // ForegroundWindow.GetCurrent es method group — SpaceSuppressor sólo
        // lo invoca tras pasar los guards baratos, así no hay syscall por
        // cada keystroke del sistema.
        var procFilter = config.IgnoreWindowFilter ? "" : config.TargetProcessName;
        var titleFilter = config.IgnoreWindowFilter ? "" : config.TargetWindowTitleContains;
        using var hook = new LowLevelKeyboardHook(vk =>
        {
            var decision = SpaceSuppressor.ShouldSuppress(
                vk,
                DateTime.UtcNow,
                tracker.LastScannerKeyTime,
                ForegroundWindow.GetCurrent,
                procFilter,
                titleFilter);
            if (vk == 0x20)
            {
                var sinceScanner = (DateTime.UtcNow - tracker.LastScannerKeyTime).TotalMilliseconds;
                AppLog.Debug($"LL hook VK=SPACE sinceScannerMs={sinceScanner:F0} decision={(decision ? "SUPPRESS" : "PASS")}");
            }
            return decision;
        });
        hook.Install();
        AppLog.Info("LowLevelKeyboardHook installed");

        using var tray = new TrayController(() =>
        {
            using var form = new SettingsForm(config, autostart, configPath);
            form.ShowDialog();
        });

        Application.Run(new ApplicationContext());
        AppLog.Info("=== shutdown ===");
    }
}
