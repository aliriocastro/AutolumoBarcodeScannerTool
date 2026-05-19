namespace AutolumoBarcodeScannerTool.Core.Models;

public sealed record ScanEvent(
    string Payload,
    ScanTerminator DetectedTerminator,
    DateTimeOffset Timestamp);
