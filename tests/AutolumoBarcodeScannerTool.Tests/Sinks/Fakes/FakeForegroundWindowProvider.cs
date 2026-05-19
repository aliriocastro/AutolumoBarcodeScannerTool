using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Tests.Sinks.Fakes;

public sealed class FakeForegroundWindowProvider : IForegroundWindowProvider
{
    public ForegroundWindowInfo? Current { get; set; }
    public ForegroundWindowInfo? GetCurrent() => Current;
}
