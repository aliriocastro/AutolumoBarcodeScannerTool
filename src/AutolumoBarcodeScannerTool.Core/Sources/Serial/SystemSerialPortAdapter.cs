using System.IO.Ports;

namespace AutolumoBarcodeScannerTool.Core.Sources.Serial;

public sealed class SystemSerialPortAdapter : ISerialPortAdapter
{
    private readonly SerialPortConfig _config;
    private SerialPort? _port;

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    public SystemSerialPortAdapter(SerialPortConfig config)
    {
        _config = config;
    }

    public bool IsOpen => _port?.IsOpen ?? false;

    public void Open()
    {
        _port = new SerialPort(_config.PortName, _config.BaudRate)
        {
            DataBits = _config.DataBits,
            Parity = Enum.Parse<Parity>(_config.Parity, ignoreCase: true),
            StopBits = Enum.Parse<StopBits>(_config.StopBits, ignoreCase: true),
            ReadTimeout = 500,
            WriteTimeout = 500
        };
        _port.DataReceived += OnPortDataReceived;
        _port.Open();
    }

    public void Close()
    {
        if (_port is null) return;
        _port.DataReceived -= OnPortDataReceived;
        if (_port.IsOpen) _port.Close();
        _port.Dispose();
        _port = null;
    }

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is null || !_port.IsOpen) return;
        try
        {
            var available = _port.BytesToRead;
            if (available <= 0) return;
            var buffer = new byte[available];
            var read = _port.Read(buffer, 0, available);
            if (read <= 0) return;
            var data = read == available ? buffer : buffer.AsMemory(0, read).ToArray();
            DataReceived?.Invoke(this, data);
        }
        catch (Exception)
        {
            // intencionalmente silencioso aquí; el source loguea state changes
        }
    }

    public void Dispose() => Close();
}
