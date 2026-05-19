namespace AutolumoBarcodeScannerTool.Core.Models;

public enum SourceType
{
    Serial,
    HidKeyboard
}

public enum ScanTerminator
{
    Cr,
    Lf,
    CrLf,
    Any
}

public enum SourceConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public enum SerialEncoding
{
    Ascii,
    Utf8,
    Latin1
}
