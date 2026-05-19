namespace AutolumoBarcodeScannerTool.Core.Sinks;

public interface IForegroundWindowProvider
{
    ForegroundWindowInfo? GetCurrent();
}

public sealed record ForegroundWindowInfo(string ProcessName, string WindowTitle);
