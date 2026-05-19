using AutolumoBarcodeScannerTool.Core.Models;

namespace AutolumoBarcodeScannerTool.Core.Sinks;

public interface IInputSink
{
    Task SendAsync(ScanEvent ev, CancellationToken ct);
}
