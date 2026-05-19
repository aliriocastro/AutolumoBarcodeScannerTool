using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Orchestration;
using AutolumoBarcodeScannerTool.Core.Transforms;
using AutolumoBarcodeScannerTool.Tests.Orchestration.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Orchestration;

public class ScannerOrchestratorTests
{
    private sealed class IdentityTransform : ITerminatorTransform
    {
        public ScanEvent Apply(ScanEvent input) => input with { Payload = input.Payload + "[X]" };
    }

    [Fact]
    public async Task StartAsync_StartsSource()
    {
        var source = new FakeInputSource();
        var sink = new FakeSink();
        var sut = new ScannerOrchestrator(source, new IdentityTransform(), sink,
            NullLogger<ScannerOrchestrator>.Instance);

        await sut.StartAsync(CancellationToken.None);

        source.StartCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ScanEvent_FromSource_FlowsThroughTransformToSink()
    {
        var source = new FakeInputSource();
        var sink = new FakeSink();
        var sut = new ScannerOrchestrator(source, new IdentityTransform(), sink,
            NullLogger<ScannerOrchestrator>.Instance);
        await sut.StartAsync(CancellationToken.None);

        var ev = new ScanEvent("ABC", ScanTerminator.CrLf, DateTimeOffset.UnixEpoch);
        await source.EmitAsync(ev);

        sink.Received.Count.ShouldBe(1);
        sink.Received[0].Payload.ShouldBe("ABC[X]");
    }

    [Fact]
    public async Task StopAsync_StopsSource()
    {
        var source = new FakeInputSource();
        var sink = new FakeSink();
        var sut = new ScannerOrchestrator(source, new IdentityTransform(), sink,
            NullLogger<ScannerOrchestrator>.Instance);
        await sut.StartAsync(CancellationToken.None);

        await sut.StopAsync(CancellationToken.None);

        source.StopCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SinkException_IsCaught_AndOrchestratorContinues()
    {
        var source = new FakeInputSource();
        var throwingSink = new ThrowingSink();
        var sut = new ScannerOrchestrator(source, new IdentityTransform(), throwingSink,
            NullLogger<ScannerOrchestrator>.Instance);
        await sut.StartAsync(CancellationToken.None);

        await Should.NotThrowAsync(() => source.EmitAsync(
            new ScanEvent("X", ScanTerminator.CrLf, DateTimeOffset.UnixEpoch)));
    }

    private sealed class ThrowingSink : Core.Sinks.IInputSink
    {
        public Task SendAsync(ScanEvent ev, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }
}
