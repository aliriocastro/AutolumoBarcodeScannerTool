namespace AutolumoBarcodeScannerTool.Core.Models;

public sealed class ScannerOptions
{
    public const string SectionName = "Scanner";

    public bool Enabled { get; set; } = true;
    public SourceType SourceType { get; set; } = SourceType.Serial;
    public ScanTerminator Terminator { get; set; } = ScanTerminator.CrLf;
    public SerialOptions Serial { get; set; } = new();
    public HidKeyboardOptions HidKeyboard { get; set; } = new();
    public TargetOptions Target { get; set; } = new();
    public OutputOptions Output { get; set; } = new();
}

public sealed class SerialOptions
{
    public string PortName { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public SerialEncoding Encoding { get; set; } = SerialEncoding.Ascii;
}

public sealed class HidKeyboardOptions
{
    public string VendorId { get; set; } = "0x0000";
    public string ProductId { get; set; } = "0x0000";
}

public sealed class TargetOptions
{
    public string ProcessName { get; set; } = "";
    public string? WindowTitleContains { get; set; }
}

public sealed class OutputOptions
{
    public OutputMode OnTerminator { get; set; } = OutputMode.Tab;
    public string OutputSuffix { get; set; } = "{TAB}";
}

public sealed class AutostartOptions
{
    public const string SectionName = "Autostart";
    public bool Enabled { get; set; } = true;
}

public sealed class LoggingOptions
{
    public const string SectionName = "Logging";
    public string MinimumLevel { get; set; } = "Information";
    public bool LogPayload { get; set; } = true;
}
