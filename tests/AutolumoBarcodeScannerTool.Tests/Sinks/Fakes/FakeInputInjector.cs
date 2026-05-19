using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Tests.Sinks.Fakes;

public sealed class FakeInputInjector : IInputInjector
{
    public List<string> SentTexts { get; } = new();
    public TimeSpan SimulatedDelay { get; set; } = TimeSpan.Zero;
    public Action<string>? OnSendingText { get; set; }
    public ManualResetEventSlim? BlockUntilSignaled { get; set; }

    public void SendText(string text)
    {
        OnSendingText?.Invoke(text);
        if (SimulatedDelay > TimeSpan.Zero) Thread.Sleep(SimulatedDelay);
        BlockUntilSignaled?.Wait();
        SentTexts.Add(text);
    }
}
