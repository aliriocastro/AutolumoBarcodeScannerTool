using System.Diagnostics;
using System.IO;
using System.Reflection;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Orchestration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutolumoBarcodeScannerTool.Tray;

internal sealed class TrayIconController : IDisposable
{
    private readonly ScannerOrchestrator _orchestrator;
    private readonly AutostartManager _autostart;
    private readonly string _configPath;
    private readonly ILogger<TrayIconController> _logger;
    private readonly IOptionsMonitor<ScannerOptions> _opts;
    private readonly NotifyIcon _icon = new();
    private readonly ContextMenuStrip _menu = new();
    private bool _enabled = true;
    private SettingsForm? _settings;

    public TrayIconController(
        ScannerOrchestrator orchestrator,
        AutostartManager autostart,
        IOptionsMonitor<ScannerOptions> opts,
        string configPath,
        ILogger<TrayIconController> logger)
    {
        _orchestrator = orchestrator;
        _autostart = autostart;
        _opts = opts;
        _configPath = configPath;
        _logger = logger;
    }

    public void Show(bool startEnabled = true)
    {
        _enabled = startEnabled;
        _icon.Text = "Autolumo Barcode Scanner Tool";
        _icon.Icon = LoadIcon(_enabled ? "tray-active.ico" : "tray-paused.ico");
        BuildMenu();
        _icon.ContextMenuStrip = _menu;
        _icon.Visible = true;
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();
        var toggle = new ToolStripMenuItem(_enabled ? "Pausar" : "Reanudar");
        toggle.Click += async (_, _) => await ToggleAsync();
        _menu.Items.Add(toggle);

        var settings = new ToolStripMenuItem("Configuración...");
        settings.Click += (_, _) => OpenSettings();
        _menu.Items.Add(settings);

        var logs = new ToolStripMenuItem("Ver logs");
        logs.Click += (_, _) => OpenLogsFolder();
        _menu.Items.Add(logs);

        _menu.Items.Add(new ToolStripSeparator());

        var quit = new ToolStripMenuItem("Salir");
        quit.Click += (_, _) => Application.Exit();
        _menu.Items.Add(quit);
    }

    private async Task ToggleAsync()
    {
        try
        {
            if (_enabled)
            {
                await _orchestrator.StopAsync(CancellationToken.None);
                _icon.Icon = LoadIcon("tray-paused.ico");
                _enabled = false;
            }
            else
            {
                await _orchestrator.StartAsync(CancellationToken.None);
                _icon.Icon = LoadIcon("tray-active.ico");
                _enabled = true;
            }
            BuildMenu();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en pausa/reanudar");
            _icon.Icon = LoadIcon("tray-error.ico");
            MessageBox.Show($"Error al cambiar estado: {ex.Message}",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.BringToFront();
            return;
        }
        _settings = new SettingsForm(_opts.CurrentValue, _autostart, _configPath);
        _settings.Show();
    }

    private void OpenLogsFolder()
    {
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutolumoBarcodeScannerTool", "logs");
        if (Directory.Exists(logsDir))
            Process.Start(new ProcessStartInfo("explorer.exe", logsDir) { UseShellExecute = true });
    }

    private static Icon LoadIcon(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        if (resName is null) return SystemIcons.Application;
        using var stream = asm.GetManifestResourceStream(resName);
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _settings?.Dispose();
    }
}
