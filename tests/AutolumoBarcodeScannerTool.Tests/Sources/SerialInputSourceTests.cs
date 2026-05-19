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
}
