using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sources;

namespace AutolumoBarcodeScannerTool.Tests.Orchestration.Fakes;

public sealed class FakeInputSource : IInputSource
{
    public event Func<ScanEvent, Task>? OnScan;
#pragma warning disable CS0067
    public event EventHandler<SourceConnectionState>? StateChanged;
#pragma warning restore CS0067

    public SourceConnectionState State { get; private set; } = SourceConnectionState.Disconnected;
    public bool StartCalled { get; private set; }
    public bool StopCalled { get; private set; }

    public Task StartAsync(CancellationToken ct) { StartCalled = true; State = SourceConnectionState.Connected; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct) { StopCalled = true; State = SourceConnectionState.Disconnected; return Task.CompletedTask; }

    public Task EmitAsync(ScanEvent ev) => OnScan?.Invoke(ev) ?? Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
