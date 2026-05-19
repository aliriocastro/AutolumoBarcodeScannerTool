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
        AppLog.Info($"=== arranque v0.2.5 — VID={config.VendorId} PID={config.ProductId} " +
                    $"verbose={config.VerboseLogging} ===");

        var autostart = new AutostartManager();

        // SpaceArbiter: tras suprimir un SPACE, espera 25 ms. Si llega otra
        // tecla en ese intervalo → era un burst (lector) → SPACE queda bloqueado.
        // Si pasan 25 ms sin actividad → era SPACE humano → restaurar via
        // SendInput con sentinel. El LL hook ya nos reconoce vía LLKHF_INJECTED.
        var arbiter = new SpaceArbiter(restoreSpace: () =>
        {
            var sent = SpaceInjector.SendSpace();
            AppLog.Debug($"Arbiter: restaurando SPACE humano (SendInput sent={sent})");
        }, delayMs: 25);

        using var hook = new LowLevelKeyboardHook((vk, flags, dwExtraInfo) =>
        {
            var suppress = SpaceSuppressor.ShouldSuppress(vk, flags, dwExtraInfo);

            if (vk == 0x20)
            {
                AppLog.Debug($"LL hook VK=SPACE flags=0x{flags:X} dwExtraInfo=0x{dwExtraInfo.ToInt64():X} decision={(suppress ? "SUPPRESS" : "PASS")}");
                if (suppress) arbiter.OnSpaceSuppressed();
                // Si !suppress es nuestra propia re-inyección con sentinel —
                // no notificamos al arbiter para evitar bucle.
            }
            else
            {
                // Cualquier otra tecla cancela una restauración pendiente:
                // el SPACE anterior era parte de un burst del lector.
                arbiter.OnNonSpaceKey();
            }

            return suppress;
        });
        hook.Install();
        AppLog.Info("LowLevelKeyboardHook installed (timing-based arbiter, delay=25ms)");

        using var tray = new TrayController(() =>
        {
            using var form = new SettingsForm(config, autostart, configPath);
            form.ShowDialog();
        });

        Application.Run(new ApplicationContext());
        AppLog.Info("=== shutdown ===");
    }
}
