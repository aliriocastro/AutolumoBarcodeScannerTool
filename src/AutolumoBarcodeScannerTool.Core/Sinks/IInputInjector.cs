namespace AutolumoBarcodeScannerTool.Core.Sinks;

public interface IInputInjector
{
    void SendText(string text);
}
