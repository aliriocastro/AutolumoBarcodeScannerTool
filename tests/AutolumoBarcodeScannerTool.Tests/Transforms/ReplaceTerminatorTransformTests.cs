using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Transforms;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Transforms;

public class ReplaceTerminatorTransformTests
{
    private static ReplaceTerminatorTransform CreateSut(OutputOptions opts) =>
        new(Options.Create(opts));

    private static ScanEvent SampleEvent(string payload = "ABC123") =>
        new(payload, ScanTerminator.CrLf, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Apply_TabMode_AppendsTabCharacter()
    {
        var sut = CreateSut(new OutputOptions { OnTerminator = OutputMode.Tab });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123\t");
    }

    [Fact]
    public void Apply_TabEnterMode_AppendsTabAndNewline()
    {
        var sut = CreateSut(new OutputOptions { OnTerminator = OutputMode.TabEnter });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123\t\n");
    }

    [Fact]
    public void Apply_TabOnlyMode_AppendsNothing()
    {
        var sut = CreateSut(new OutputOptions { OnTerminator = OutputMode.TabOnly });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123");
    }

    [Fact]
    public void Apply_CustomMode_ResolvesTabAndEnterTokens()
    {
        var sut = CreateSut(new OutputOptions
        {
            OnTerminator = OutputMode.Custom,
            OutputSuffix = "{TAB}suffix{ENTER}"
        });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123\tsuffix\n");
    }

    [Fact]
    public void Apply_CustomMode_LiteralStringWithoutTokens()
    {
        var sut = CreateSut(new OutputOptions
        {
            OnTerminator = OutputMode.Custom,
            OutputSuffix = "|END"
        });
        var result = sut.Apply(SampleEvent());

        result.Payload.ShouldBe("ABC123|END");
    }

    [Fact]
    public void Apply_PreservesTerminatorAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var input = new ScanEvent("X", ScanTerminator.Lf, ts);
        var sut = CreateSut(new OutputOptions { OnTerminator = OutputMode.Tab });

        var result = sut.Apply(input);

        result.DetectedTerminator.ShouldBe(ScanTerminator.Lf);
        result.Timestamp.ShouldBe(ts);
    }
}
