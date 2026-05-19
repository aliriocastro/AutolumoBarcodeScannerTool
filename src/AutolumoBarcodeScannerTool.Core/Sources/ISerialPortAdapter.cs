using System.Text;

namespace AutolumoBarcodeScannerTool.Core.Sources;

public interface ISerialPortAdapter : IDisposable
{
    event EventHandler<ReadOnlyMemory<byte>>? DataReceived;
    bool IsOpen { get; }
    void Open();
    void Close();
}

public sealed record SerialPortConfig(
    string PortName,
    int BaudRate,
    int DataBits,
    string Parity,
    string StopBits,
    Encoding Encoding);
