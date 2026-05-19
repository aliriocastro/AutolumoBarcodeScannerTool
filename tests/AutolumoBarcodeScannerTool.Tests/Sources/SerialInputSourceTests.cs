using System.Text;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sources;
using AutolumoBarcodeScannerTool.Core.Sources.Serial;
using AutolumoBarcodeScannerTool.Tests.Sources.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Sources;

public class SerialInputSourceTests
{
    private static (SerialInputSource Sut, FakeSerialPortAdapter Port, List<ScanEvent> Events)
        Build(ScanTerminator terminator, Encoding? encoding = null)
    {
        var port = new FakeSerialPortAdapter();
        var sut = new SerialInputSource(port, terminator, encoding ?? Encoding.ASCII,
            NullLogger<SerialInputSource>.Instance);
        var events = new List<ScanEvent>();
        sut.OnScan += ev => { events.Add(ev); return Task.CompletedTask; };
        sut.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return (sut, port, events);
    }

    [Fact]
    public void Emits_ScanEvent_OnCrLfTerminator()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf);

        port.EmitText("ABC123\r\n", Encoding.ASCII);

        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("ABC123");
        events[0].DetectedTerminator.ShouldBe(ScanTerminator.CrLf);
    }

    [Fact]
    public void Emits_OnCrOnly_WhenTerminatorCr()
    {
        var (_, port, events) = Build(ScanTerminator.Cr);
        port.EmitText("HELLO\r", Encoding.ASCII);

        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("HELLO");
        events[0].DetectedTerminator.ShouldBe(ScanTerminator.Cr);
    }

    [Fact]
    public void Emits_OnLfOnly_WhenTerminatorLf()
    {
        var (_, port, events) = Build(ScanTerminator.Lf);
        port.EmitText("HELLO\n", Encoding.ASCII);

        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("HELLO");
        events[0].DetectedTerminator.ShouldBe(ScanTerminator.Lf);
    }

    [Fact]
    public void AnyTerminator_PrefersCrLfOverIndividualCrLf()
    {
        var (_, port, events) = Build(ScanTerminator.Any);
        port.EmitText("A\r\nB\rC\n", Encoding.ASCII);

        events.Count.ShouldBe(3);
        events[0].Payload.ShouldBe("A");
        events[0].DetectedTerminator.ShouldBe(ScanTerminator.CrLf);
        events[1].Payload.ShouldBe("B");
        events[1].DetectedTerminator.ShouldBe(ScanTerminator.Cr);
        events[2].Payload.ShouldBe("C");
        events[2].DetectedTerminator.ShouldBe(ScanTerminator.Lf);
    }

    [Fact]
    public void Buffers_PartialPayload_AcrossMultipleEmissions()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf);

        port.EmitText("ABC", Encoding.ASCII);
        events.ShouldBeEmpty();

        port.EmitText("123\r\n", Encoding.ASCII);
        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("ABC123");
    }

    [Fact]
    public void EmitsMultipleEvents_InSingleBurst()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf);
        port.EmitText("ONE\r\nTWO\r\n", Encoding.ASCII);

        events.Count.ShouldBe(2);
        events[0].Payload.ShouldBe("ONE");
        events[1].Payload.ShouldBe("TWO");
    }

    [Fact]
    public void Decodes_Latin1_Correctly()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf, Encoding.Latin1);
        var bytes = Encoding.Latin1.GetBytes("ñandú\r\n");
        port.EmitBytes(bytes);

        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("ñandú");
    }

    [Fact]
    public void CrLfTerminator_DoesNotEmit_OnSoloCrWithoutLf()
    {
        var (_, port, events) = Build(ScanTerminator.CrLf);
        port.EmitText("ABC\r", Encoding.ASCII);
        events.ShouldBeEmpty();

        port.EmitText("\n", Encoding.ASCII);
        events.Count.ShouldBe(1);
        events[0].Payload.ShouldBe("ABC");
    }
}
