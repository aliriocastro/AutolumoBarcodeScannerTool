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
        var (sut, window, injector) = Build("MiAppContable");
        window.Current = new ForegroundWindowInfo("MiAppContable", "x");
        injector.SimulatedDelay = TimeSpan.FromMilliseconds(200);

        var first = sut.SendAsync(Event("FIRST\t"), CancellationToken.None);
        // Asegurar que el primero haya entrado al semaforo
        await Task.Delay(50);
        var second = sut.SendAsync(Event("SECOND\t"), CancellationToken.None);

        await Task.WhenAll(first, second);

        injector.SentTexts.ShouldBe(new[] { "FIRST\t" });
    }
}
