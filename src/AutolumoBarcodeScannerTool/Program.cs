using System.IO;
using System.Threading;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core;
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

        // Config lives in %LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.ini.
        // On first run we seed it from appsettings.default.ini next to the exe.
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutolumoBarcodeScannerTool");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "appsettings.ini");
        if (!File.Exists(configPath))
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "appsettings.default.ini");
            if (File.Exists(seedPath)) File.Copy(seedPath, configPath);
        }

        var config = ConfigIo.Load(configPath);
        var autostart = new AutostartManager();

        // Tracker registers a hidden Raw Input window for the configured VID/PID.
        // The window MUST be created on the STA Main thread that owns Application.Run().
        using var tracker = new ScannerKeyTracker(config.VendorId, config.ProductId);
        try
        {
            tracker.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo registrar el lector HID (VID={config.VendorId}, PID={config.ProductId}):\n\n{ex.Message}\n\n" +
                "Abre Configurar… desde la bandeja para seleccionar otro dispositivo.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            // We continue running — user can fix config from the tray.
        }

        // LL hook callback: combine tracker timestamp + foreground window check.
        // ForegroundWindow.GetCurrent is passed as a method group — SpaceSuppressor
        // only invokes it after the cheap vk/time guards pass, so non-space
        // keystrokes never trigger the Win32 + Process.GetProcessById syscall.
        using var hook = new LowLevelKeyboardHook(vk => SpaceSuppressor.ShouldSuppress(
            vk,
            DateTime.UtcNow,
            tracker.LastScannerKeyTime,
            ForegroundWindow.GetCurrent,
            config.TargetProcessName,
            config.TargetWindowTitleContains));
        hook.Install();

        using var tray = new TrayController(() =>
        {
            using var form = new SettingsForm(config, autostart, configPath);
            form.ShowDialog();
        });

        Application.Run(new ApplicationContext());
    }
}
