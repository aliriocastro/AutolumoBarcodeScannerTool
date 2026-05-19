using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sinks;
using AutolumoBarcodeScannerTool.Tests.Sinks.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Sinks;

public class ForegroundProcessSinkTests
{
    private static (ForegroundProcessSink Sut, FakeForegroundWindowProvider Window, FakeInputInjector Injector)
        Build(string targetProcess, string? titleContains = null)
    {
        var opts = Options.Create(new TargetOptions
        {
            ProcessName = targetProcess,
            WindowTitleContains = titleContains
        });
        var window = new FakeForegroundWindowProvider();
        var injector = new FakeInputInjector();
        var sut = new ForegroundProcessSink(opts, window, injector, NullLogger<ForegroundProcessSink>.Instance);
        return (sut, window, injector);
    }

    private static ScanEvent Event(string payload = "ABC\t") =>
        new(payload, ScanTerminator.CrLf, DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Sends_WhenForegroundMatchesTargetProcess()
    {
        var (sut, window, injector) = Build("MiAppContable");
        window.Current = new ForegroundWindowInfo("MiAppContable", "Factura nueva");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBe(new[] { "ABC\t" });
    }

    [Fact]
    public async Task Discards_WhenForegroundIsDifferentProcess()
    {
        var (sut, window, injector) = Build("MiAppContable");
        window.Current = new ForegroundWindowInfo("notepad", "Untitled");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Discards_WhenNoForegroundWindow()
    {
        var (sut, _, injector) = Build("MiAppContable");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessNameMatch_IsCaseInsensitive()
    {
        var (sut, window, injector) = Build("MiAppContable");
        window.Current = new ForegroundWindowInfo("MIAPPCONTABLE", "x");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBe(new[] { "ABC\t" });
    }

    [Fact]
    public async Task TitleFilter_RequiresContains()
    {
        var (sut, window, injector) = Build("MiAppContable", titleContains: "Factura");
        window.Current = new ForegroundWindowInfo("MiAppContable", "Reportes mensuales");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task TitleFilter_MatchesSubstringCaseInsensitive()
    {
        var (sut, window, injector) = Build("MiAppContable", titleContains: "factura");
        window.Current = new ForegroundWindowInfo("MiAppContable", "Editar Factura 1234");

        await sut.SendAsync(Event(), CancellationToken.None);

        injector.SentTexts.ShouldBe(new[] { "ABC\t" });
    }

    [Fact]
    public async Task ConcurrentSend_SecondCallIsDiscarded()
    {
        // Deterministic synchronization: block the FIRST injector call until
        // we manually release it. This avoids timing-based flakiness in CI.
        var (sut, window, injector) = Build("MiAppContable");
        window.Current = new ForegroundWindowInfo("MiAppContable", "x");

        var firstEnteredInjector = new TaskCompletionSource();
        using var releaseFirst = new ManualResetEventSlim(initialState: false);
        injector.OnSendingText = _ => firstEnteredInjector.TrySetResult();
        injector.BlockUntilSignaled = releaseFirst;

        // Kick off FIRST. It will acquire the semaphore synchronously, then
        // hand off to Task.Run where it'll block inside the injector.
        var first = sut.SendAsync(Event("FIRST\t"), CancellationToken.None);

        // Wait deterministically for FIRST to actually enter the injector,
        // proving the semaphore is held.
        await firstEnteredInjector.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // SECOND should fail to acquire the semaphore and return immediately.
        await sut.SendAsync(Event("SECOND\t"), CancellationToken.None);

        // Release FIRST and let it finish.
        releaseFirst.Set();
        await first;

        injector.SentTexts.ShouldBe(new[] { "FIRST\t" });
    }
}
