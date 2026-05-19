using System.IO;
using System.Threading;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core;
using AutolumoBarcodeScannerTool.Diag;
using AutolumoBarcodeScannerTool.Hid;
using AutolumoBarcodeScannerTool.Tray;

namespace AutolumoBarcodeScannerTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(initiallyOwned: true,
            name: @"Local\AutolumoBarcodeScannerTool_v1", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show(
                "Autolumo ya está en ejecución. Revisa la bandeja del sistema.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutolumoBarcodeScannerTool");
        var logDir = Path.Combine(localData, "logs");
        Directory.CreateDirectory(localData);
        var configPath = Path.Combine(localData, "appsettings.ini");
        if (!File.Exists(configPath))
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "appsettings.default.ini");
            if (File.Exists(seedPath)) File.Copy(seedPath, configPath);
        }

        var config = ConfigIo.Load(configPath);
        AppLog.Init(logDir, config.VerboseLogging);
        AppLog.Info($"=== arranque v0.2.3 — VID={config.VendorId} PID={config.ProductId} " +
                    $"verbose={config.VerboseLogging} ===");

        var autostart = new AutostartManager();

        // Tracker registra un hidden window para Raw Input de TODOS los teclados.
        // Cuando llega un SPACE, identifica el origen y re-inyecta si proviene
        // del teclado humano (el LL hook suprime preventivamente todo SPACE no
        // marcado con el sentinel).
        using var tracker = new ScannerKeyTracker(config.VendorId, config.ProductId);
        try
        {
            tracker.Start();
            AppLog.Info($"ScannerKeyTracker.Start OK — filtrando VID_{config.VendorId}&PID_{config.ProductId}");
        }
        catch (Exception ex)
        {
            AppLog.Info($"ScannerKeyTracker.Start FAILED: {ex.Message}");
            MessageBox.Show(
                $"No se pudo registrar el lector HID (VID={config.VendorId}, PID={config.ProductId}):\n\n{ex.Message}\n\n" +
                "Abre Configurar… desde la bandeja para seleccionar otro dispositivo.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // LL hook: suprime preventivamente cualquier VK_SPACE no marcado por
        // nosotros (sentinel). El WndProc del tracker es quien decide después
        // si re-inyectarlo (era humano) o dejarlo bloqueado (era del lector).
        using var hook = new LowLevelKeyboardHook((vk, dwExtraInfo) =>
        {
            var decision = SpaceSuppressor.ShouldSuppress(vk, dwExtraInfo);
            if (vk == 0x20)
                AppLog.Debug($"LL hook VK=SPACE dwExtraInfo=0x{dwExtraInfo.ToInt64():X} decision={(decision ? "SUPPRESS" : "PASS")}");
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
