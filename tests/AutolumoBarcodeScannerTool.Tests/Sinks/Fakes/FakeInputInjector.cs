using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Tests.Sinks.Fakes;

public sealed class FakeInputInjector : IInputInjector
{
    public List<string> SentTexts { get; } = new();
    public TimeSpan SimulatedDelay { get; set; } = TimeSpan.Zero;

    public void SendText(string text)
    {
        if (SimulatedDelay > TimeSpan.Zero) Thread.Sleep(SimulatedDelay);
        SentTexts.Add(text);
    }
}
