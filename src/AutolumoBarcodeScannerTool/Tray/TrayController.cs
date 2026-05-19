namespace AutolumoBarcodeScannerTool.Tray;

internal sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Action _openSettings;

    public TrayController(Action openSettings)
    {
        _openSettings = openSettings;
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Autolumo — bloqueador de espacios del lector",
            ContextMenuStrip = BuildMenu()
        };
        _icon.DoubleClick += (_, _) => _openSettings();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Configurar…", null, (_, _) => _openSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Application.Exit());
        return menu;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
