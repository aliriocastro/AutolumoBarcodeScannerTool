using AutolumoBarcodeScannerTool.Core.Models;

namespace AutolumoBarcodeScannerTool.Core.Sources;

public interface IInputSource : IAsyncDisposable
{
    event Func<ScanEvent, Task>? OnScan;
    event EventHandler<SourceConnectionState>? StateChanged;
    SourceConnectionState State { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
