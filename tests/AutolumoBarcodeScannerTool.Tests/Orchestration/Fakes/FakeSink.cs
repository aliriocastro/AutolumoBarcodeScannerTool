using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Tests.Orchestration.Fakes;

public sealed class FakeSink : IInputSink
{
    public List<ScanEvent> Received { get; } = new();

    public Task SendAsync(ScanEvent ev, CancellationToken ct)
    {
        Received.Add(ev);
        return Task.CompletedTask;
    }
}
