using System.IO.Ports;
using AutolumoBarcodeScannerTool.Autostart;
using AutolumoBarcodeScannerTool.Core.Configuration;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Hid;

namespace AutolumoBarcodeScannerTool.Tray;

internal sealed class SettingsForm : Form
{
    private readonly ScannerOptions _opts;
    private readonly AutostartManager _autostart;
    private readonly string _configPath;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    private readonly ComboBox _sourceType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _terminator = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _enabled = new() { Text = "Servicio habilitado", AutoSize = true };
    private readonly CheckBox _autostartChk = new() { Text = "Autoarranque al iniciar sesión", AutoSize = true };

    private readonly ComboBox _portName = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _baudRate = new();
    private readonly ComboBox _parity = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _stopBits = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _encoding = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly ComboBox _hidDevice = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _vid = new();
    private readonly TextBox _pid = new();

    private readonly TextBox _processName = new();
    private readonly TextBox _windowTitleContains = new();

    private readonly ComboBox _outputMode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _outputSuffix = new();

    private readonly Button _save = new() { Text = "Guardar", Width = 100 };
    private readonly Button _cancel = new() { Text = "Cancelar", Width = 100 };

    public SettingsForm(ScannerOptions opts, AutostartManager autostart, string configPath)
    {
        _opts = opts;
        _autostart = autostart;
        _configPath = configPath;

        Text = "Autolumo Barcode Scanner Tool — Configuración";
        Width = 560; Height = 480;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        BuildTabs();
        BuildButtons();
        LoadValues();
    }

    private void BuildTabs()
    {
        var general = new TabPage("General");
        general.Controls.Add(Stack(8,
            _enabled,
            _autostartChk,
            Label("Tipo de fuente:"), _sourceType,
            Label("Terminador:"), _terminator
        ));
        _sourceType.Items.AddRange(new object[] { "Serial", "HidKeyboard" });
        _terminator.Items.AddRange(new object[] { "Cr", "Lf", "CrLf", "Any" });

        var serial = new TabPage("Serial");
        _portName.Items.AddRange(SerialPort.GetPortNames());
        _parity.Items.AddRange(Enum.GetNames<Parity>());
        _stopBits.Items.AddRange(Enum.GetNames<StopBits>());
        _encoding.Items.AddRange(new object[] { "Ascii", "Utf8", "Latin1" });
        serial.Controls.Add(Stack(8,
            Label("Puerto:"), _portName,
            Label("Baud rate:"), _baudRate,
            Label("Parity:"), _parity,
            Label("Stop bits:"), _stopBits,
            Label("Encoding:"), _encoding
        ));

        var hid = new TabPage("HID");
        var hidWarning = new Label
        {
            Text = "⚠ Modo HID-keyboard: la supresión del input crudo del lector es " +
                   "best-effort (heurística de timing). Para garantía total, configure " +
                   "su lector en modo Serial/USB-CDC mediante el código de barras de " +
                   "configuración de fábrica (consulte el manual del lector).",
            AutoSize = false,
            Width = 480,
            Height = 80,
            ForeColor = Color.DarkRed
        };
        hid.Controls.Add(Stack(8,
            hidWarning,
            Label("Dispositivo detectado:"), _hidDevice,
            Label("VID (hex):"), _vid,
            Label("PID (hex):"), _pid
        ));
        _hidDevice.SelectedIndexChanged += (_, _) =>
        {
            if (_hidDevice.SelectedItem is HidDeviceDescriptor d)
            {
                _vid.Text = d.VendorId; _pid.Text = d.ProductId;
            }
        };
        foreach (var d in HidDeviceEnumerator.Enumerate())
            _hidDevice.Items.Add(d);

        var target = new TabPage("Destino");
        target.Controls.Add(Stack(8,
            Label("Nombre de proceso (sin .exe):"), _processName,
            Label("Contiene en título (opcional):"), _windowTitleContains
        ));

        var output = new TabPage("Salida");
        _outputMode.Items.AddRange(new object[] { "Tab", "TabEnter", "TabOnly", "Custom" });
        output.Controls.Add(Stack(8,
            Label("Modo al terminador:"), _outputMode,
            Label("Sufijo (Custom; tokens {TAB} {ENTER}):"), _outputSuffix
        ));

        _tabs.TabPages.AddRange(new[] { general, serial, hid, target, output });
        Controls.Add(_tabs);
    }

    private void BuildButtons()
    {
        _save.Click += (_, _) => Save();
        _cancel.Click += (_, _) => Close();
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };
        bar.Controls.Add(_save);
        bar.Controls.Add(_cancel);
        Controls.Add(bar);
    }

    private void LoadValues()
    {
        _enabled.Checked = _opts.Enabled;
        _autostartChk.Checked = _autostart.IsEnabled();
        _sourceType.SelectedItem = _opts.SourceType.ToString();
        _terminator.SelectedItem = _opts.Terminator.ToString();

        _portName.Text = _opts.Serial.PortName;
        _baudRate.Text = _opts.Serial.BaudRate.ToString();
        _parity.SelectedItem = _opts.Serial.Parity;
        _stopBits.SelectedItem = _opts.Serial.StopBits;
        _encoding.SelectedItem = _opts.Serial.Encoding.ToString();

        _vid.Text = _opts.HidKeyboard.VendorId;
        _pid.Text = _opts.HidKeyboard.ProductId;

        _processName.Text = _opts.Target.ProcessName;
        _windowTitleContains.Text = _opts.Target.WindowTitleContains ?? "";

        _outputMode.SelectedItem = _opts.Output.OnTerminator.ToString();
        _outputSuffix.Text = _opts.Output.OutputSuffix;
    }

    private void Save()
    {
        try
        {
            var contents = File.ReadAllText(_configPath);
            contents = IniConfigWriter.Update(contents, "Scanner:Enabled", _enabled.Checked ? "true" : "false");
            contents = IniConfigWriter.Update(contents, "Scanner:SourceType", _sourceType.SelectedItem?.ToString() ?? "Serial");
            contents = IniConfigWriter.Update(contents, "Scanner:Terminator", _terminator.SelectedItem?.ToString() ?? "CrLf");
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:PortName", _portName.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:BaudRate", _baudRate.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:Parity", _parity.SelectedItem?.ToString() ?? "None");
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:StopBits", _stopBits.SelectedItem?.ToString() ?? "One");
            contents = IniConfigWriter.Update(contents, "Scanner:Serial:Encoding", _encoding.SelectedItem?.ToString() ?? "Ascii");
            contents = IniConfigWriter.Update(contents, "Scanner:HidKeyboard:VendorId", _vid.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:HidKeyboard:ProductId", _pid.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Target:ProcessName", _processName.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Target:WindowTitleContains", _windowTitleContains.Text);
            contents = IniConfigWriter.Update(contents, "Scanner:Output:OnTerminator", _outputMode.SelectedItem?.ToString() ?? "Tab");
            contents = IniConfigWriter.Update(contents, "Scanner:Output:OutputSuffix", _outputSuffix.Text);
            File.WriteAllText(_configPath, contents);

            // Environment.ProcessPath returns the launcher .exe even for single-file
            // self-contained publishes. Application.ExecutablePath in that scenario
            // returns the extraction temp folder which doesn't survive reboots.
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
            if (_autostartChk.Checked) _autostart.Enable(exePath);
            else _autostart.Disable();

            MessageBox.Show(
                "Configuración guardada.\n\n" +
                "• Proceso destino y título: se aplican inmediatamente.\n" +
                "• Tipo de fuente, puerto COM, VID/PID HID, encoding, terminador y baud rate: " +
                "requieren reiniciar la aplicación.",
                "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Autolumo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Label Label(string text) => new() { Text = text, AutoSize = true };
    private static FlowLayoutPanel Stack(int gap, params Control[] children)
    {
        var p = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12)
        };
        foreach (var c in children)
        {
            c.Margin = new Padding(0, 0, 0, gap);
            if (c is TextBox or ComboBox) c.Width = 480;
            p.Controls.Add(c);
        }
        return p;
    }
}
