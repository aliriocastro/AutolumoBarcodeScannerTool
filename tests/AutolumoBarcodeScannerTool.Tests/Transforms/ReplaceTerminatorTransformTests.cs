using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Transforms;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Transforms;

public class ReplaceTerminatorTransformTests
{
    private static ScanEvent Sample(string payload) =>
        new(payload, ScanTerminator.CrLf, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Apply_StripsFirstSpaceAndAppendsNewline()
    {
        var sut = new ReplaceTerminatorTransform();

        var result = sut.Apply(Sample("LAB 2026-001"));

        result.Payload.ShouldBe("LAB2026-001\n");
    }

    [Fact]
    public void Apply_OnlyFirstSpaceIsStripped()
    {
        var sut = new ReplaceTerminatorTransform();

        var result = sut.Apply(Sample("AB CD EF"));

        result.Payload.ShouldBe("ABCD EF\n");
    }

    [Fact]
    public void Apply_PayloadWithoutSpace_OnlyAppendsNewline()
    {
        var sut = new ReplaceTerminatorTransform();

        var result = sut.Apply(Sample("ABC123"));

        result.Payload.ShouldBe("ABC123\n");
    }

    [Fact]
    public void Apply_EmptyPayload_OnlyNewline()
    {
        var sut = new ReplaceTerminatorTransform();

        var result = sut.Apply(Sample(string.Empty));

        result.Payload.ShouldBe("\n");
    }

    [Fact]
    public void Apply_LeadingSpace_IsStripped()
    {
        var sut = new ReplaceTerminatorTransform();

        var result = sut.Apply(Sample(" ABC"));

        result.Payload.ShouldBe("ABC\n");
    }

    [Fact]
    public void Apply_PreservesTerminatorAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var input = new ScanEvent("X Y", ScanTerminator.Lf, ts);
        var sut = new ReplaceTerminatorTransform();

        var result = sut.Apply(input);

        result.Payload.ShouldBe("XY\n");
        result.DetectedTerminator.ShouldBe(ScanTerminator.Lf);
        result.Timestamp.ShouldBe(ts);
    }
}
