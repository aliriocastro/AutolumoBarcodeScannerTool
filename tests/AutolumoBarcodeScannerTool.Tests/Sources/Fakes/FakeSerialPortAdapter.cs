using AutolumoBarcodeScannerTool.Core.Sources;

namespace AutolumoBarcodeScannerTool.Tests.Sources.Fakes;

public sealed class FakeSerialPortAdapter : ISerialPortAdapter
{
    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;
    public bool IsOpen { get; private set; }
    public bool OpenCalled { get; private set; }
    public bool CloseCalled { get; private set; }

    public void Open() { OpenCalled = true; IsOpen = true; }
    public void Close() { CloseCalled = true; IsOpen = false; }

    public void EmitBytes(byte[] data) => DataReceived?.Invoke(this, data);
    public void EmitText(string text, System.Text.Encoding encoding)
        => EmitBytes(encoding.GetBytes(text));

    public void Dispose() => Close();
}
