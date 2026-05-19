using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core;
using AutolumoBarcodeScannerTool.Hid;

namespace AutolumoBarcodeScannerTool.Tray;

internal sealed class SettingsForm : Form
{
    private readonly ScannerConfig _initial;
    private readonly AutostartManager _autostart;
    private readonly string _configPath;

    private readonly ComboBox _hidDevice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 480 };
    private readonly TextBox _vid = new() { Width = 100, ReadOnly = true };
    private readonly TextBox _pid = new() { Width = 100, ReadOnly = true };
    private readonly CheckBox _autostartChk = new() { Text = "Iniciar con Windows", AutoSize = true };
    private readonly CheckBox _verboseChk = new() { Text = "Diagnóstico verbose (loggea cada SPACE)", AutoSize = true };
    private readonly Button _openLogs = new() { Text = "Abrir carpeta de logs", Width = 200 };
    private readonly Button _save = new() { Text = "Guardar", Width = 100 };
    private readonly Button _cancel = new() { Text = "Cancelar", Width = 100 };

    public SettingsForm(ScannerConfig initial, AutostartManager autostart, string configPath)
    {
        _initial = initial;
        _autostart = autostart;
        _configPath = configPath;

        Text = "Autolumo — Configuración";
        Width = 560; Height = 460;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        Build();
        LoadValues();
    }

    private void Build()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16)
        };

        panel.Controls.Add(Header("Dispositivo lector"));
        panel.Controls.Add(Label("Detectados en este equipo:"));
        panel.Controls.Add(_hidDevice);

        var vidPidRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 12)
        };
        vidPidRow.Controls.Add(new Label { Text = "VID:", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
        vidPidRow.Controls.Add(_vid);
        vidPidRow.Controls.Add(new Label { Text = "  PID:", AutoSize = true, Padding = new Padding(8, 6, 4, 0) });
        vidPidRow.Controls.Add(_pid);
        panel.Controls.Add(vidPidRow);

        panel.Controls.Add(new Label
        {
            Text = "Los espacios que vengan SÓLO de este dispositivo se eliminan.\r\n" +
                   "Los espacios del teclado humano siguen llegando intactos a cualquier app.",
            ForeColor = Color.DimGray,
            AutoSize = false,
            Width = 480,
            Height = 40,
            Margin = new Padding(0, 0, 0, 12)
        });

        panel.Controls.Add(Header("Arranque"));
        panel.Controls.Add(_autostartChk);

        panel.Controls.Add(Header("Diagnóstico"));
        panel.Controls.Add(_verboseChk);
        panel.Controls.Add(new Label
        {
            Text = "Logs en %LOCALAPPDATA%\\AutolumoBarcodeScannerTool\\logs\\app-AAAAMMDD.log",
            ForeColor = Color.DimGray,
            AutoSize = false,
            Width = 480,
            Height = 18,
            Margin = new Padding(0, 4, 0, 4)
        });
        _openLogs.Click += (_, _) => OpenLogsFolder();
        panel.Controls.Add(_openLogs);

        Controls.Add(panel);

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(16, 8, 16, 8)
        };
        _save.Click += (_, _) => Save();
        _cancel.Click += (_, _) => Close();
        bar.Controls.Add(_save);
        bar.Controls.Add(_cancel);
        Controls.Add(bar);

        _hidDevice.SelectedIndexChanged += (_, _) =>
        {
            if (_hidDevice.SelectedItem is HidDeviceDescriptor d)
            {
                _vid.Text = d.VendorId;
                _pid.Text = d.ProductId;
            }
        };
    }

    private void LoadValues()
    {
        foreach (var d in HidDeviceEnumerator.Enumerate())
            _hidDevice.Items.Add(d);

        var savedVid = NormalizeHex(_initial.VendorId);
        var savedPid = NormalizeHex(_initial.ProductId);
        for (var i = 0; i < _hidDevice.Items.Count; i++)
        {
            if (_hidDevice.Items[i] is HidDeviceDescriptor d &&
                string.Equals(NormalizeHex(d.VendorId), savedVid, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeHex(d.ProductId), savedPid, StringComparison.OrdinalIgnoreCase))
            {
                _hidDevice.SelectedIndex = i;
                break;
            }
        }

        _vid.Text = savedVid;
        _pid.Text = savedPid;
        _autostartChk.Checked = _autostart.IsEnabled();
        _verboseChk.Checked = _initial.VerboseLogging;
    }

    private static void OpenLogsFolder()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutolumoBarcodeScannerTool", "logs");
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true
        });
    }

    private void Save()
    {
        try
        {
            var config = new ScannerConfig(
                VendorId: _vid.Text.Trim(),
                ProductId: _pid.Text.Trim(),
                AutostartEnabled: _autostartChk.Checked,
                VerboseLogging: _verboseChk.Checked);

            ConfigIo.Save(_configPath, config);

            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
            if (config.AutostartEnabled) _autostart.Enable(exePath);
            else _autostart.Disable();

            MessageBox.Show(
                "Configuración guardada.\n\nReinicia la aplicación para aplicar los cambios.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Autolumo",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Label Header(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(Control.DefaultFont, FontStyle.Bold),
        Margin = new Padding(0, 8, 0, 6)
    };

    private static Label Label(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 2)
    };

    private static string NormalizeHex(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        return t.ToUpperInvariant().PadLeft(4, '0');
    }
}
