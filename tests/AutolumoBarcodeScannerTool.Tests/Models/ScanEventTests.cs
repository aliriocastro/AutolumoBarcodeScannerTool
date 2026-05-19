using AutolumoBarcodeScannerTool.Core.Models;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Models;

public class ScanEventTests
{
    [Fact]
    public void Construct_PreservesAllFields()
    {
        var ts = new DateTimeOffset(2026, 5, 18, 10, 30, 0, TimeSpan.Zero);
        var ev = new ScanEvent("ABC123", ScanTerminator.CrLf, ts);

        ev.Payload.ShouldBe("ABC123");
        ev.DetectedTerminator.ShouldBe(ScanTerminator.CrLf);
        ev.Timestamp.ShouldBe(ts);
    }

    [Fact]
    public void Records_WithPayload_ReplacesPayloadKeepsRest()
    {
        var ts = DateTimeOffset.UtcNow;
        var original = new ScanEvent("ABC", ScanTerminator.CrLf, ts);

        var modified = original with { Payload = "ABC\t" };

        modified.Payload.ShouldBe("ABC\t");
        modified.DetectedTerminator.ShouldBe(ScanTerminator.CrLf);
        modified.Timestamp.ShouldBe(ts);
    }
}
