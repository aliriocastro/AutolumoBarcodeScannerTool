using AutolumoBarcodeScannerTool.Core;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests;

public class SpaceSuppressorTests
{
    private static readonly DateTime Now = new(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ScannerRecent = Now.AddMilliseconds(-50);
    private static readonly DateTime ScannerOld = Now.AddMilliseconds(-500);
    private static readonly ForegroundWindowInfo MatchingWindow = new("MiAppContable", "Factura nueva");

    private static Func<ForegroundWindowInfo?> Fg(ForegroundWindowInfo? value) => () => value;

    [Fact]
    public void Pass_When_VkIsNotSpace()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x41, // 'A'
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: Fg(MatchingWindow),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_ScannerActivityIsOld()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20, // SPACE
            now: Now,
            lastScannerKey: ScannerOld,
            foreground: Fg(MatchingWindow),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_ForegroundIsNull()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: Fg(null),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_ProcessNameDoesNotMatch()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: Fg(new ForegroundWindowInfo("notepad", "Untitled")),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_TitleDoesNotContainNeedle()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: Fg(new ForegroundWindowInfo("MiAppContable", "Reportes mensuales")),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "Factura");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Suppress_When_AllConditionsMet()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: Fg(MatchingWindow),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "Factura");

        result.ShouldBeTrue();
    }

    [Fact]
    public void Suppress_When_BothFiltersEmpty_AndScannerRecent()
    {
        // Empty filters = "apply in any foreground window". Defensive but useful default.
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: Fg(new ForegroundWindowInfo("anything", "anywhere")),
            targetProcessName: "",
            targetWindowTitleContains: "");

        result.ShouldBeTrue();
    }

    [Fact]
    public void ProcessMatch_IsCaseInsensitive()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: Fg(new ForegroundWindowInfo("MIAPPCONTABLE", "x")),
            targetProcessName: "miappcontable",
            targetWindowTitleContains: "");

        result.ShouldBeTrue();
    }

    [Fact]
    public void TitleContains_IsCaseInsensitive()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: Fg(new ForegroundWindowInfo("MiAppContable", "Editar FACTURA 1234")),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "factura");

        result.ShouldBeTrue();
    }

    [Fact]
    public void ScannerWindowBoundary_99ms_IsRecent()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: Now.AddMilliseconds(-99),
            foreground: Fg(MatchingWindow),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeTrue();
    }

    [Fact]
    public void ScannerWindowBoundary_100ms_IsOld()
    {
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: Now.AddMilliseconds(-100),
            foreground: Fg(MatchingWindow),
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_OnlyTitleConfigured_AndMatches()
    {
        // ProcessName empty, only title filter active.
        var result = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: Fg(new ForegroundWindowInfo("anything", "Bloc de notas - factura.txt")),
            targetProcessName: "",
            targetWindowTitleContains: "factura");

        result.ShouldBeTrue();
    }

    [Fact]
    public void ForegroundFactory_NotCalled_WhenVkIsNotSpace()
    {
        // Guard against perf regression: the LL hook fires on every keydown in the
        // system. ForegroundWindow.GetCurrent() is a syscall + Process.GetProcessById,
        // which is too slow to run on non-space keys.
        var called = false;
        Func<ForegroundWindowInfo?> trackingFactory = () =>
        {
            called = true;
            return MatchingWindow;
        };

        _ = SpaceSuppressor.ShouldSuppress(
            vk: 0x41, // 'A' — not space
            now: Now,
            lastScannerKey: ScannerRecent,
            foreground: trackingFactory,
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        called.ShouldBeFalse();
    }

    [Fact]
    public void ForegroundFactory_NotCalled_WhenScannerActivityIsOld()
    {
        // Even for spaces, if no recent scanner activity we skip the foreground
        // query — the human is typing alone.
        var called = false;
        Func<ForegroundWindowInfo?> trackingFactory = () =>
        {
            called = true;
            return MatchingWindow;
        };

        _ = SpaceSuppressor.ShouldSuppress(
            vk: 0x20,
            now: Now,
            lastScannerKey: ScannerOld,
            foreground: trackingFactory,
            targetProcessName: "MiAppContable",
            targetWindowTitleContains: "");

        called.ShouldBeFalse();
    }
}
