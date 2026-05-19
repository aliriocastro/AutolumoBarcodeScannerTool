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
}
