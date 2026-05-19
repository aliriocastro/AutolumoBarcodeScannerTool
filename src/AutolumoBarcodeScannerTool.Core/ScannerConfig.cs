namespace AutolumoBarcodeScannerTool.Core;

public sealed record ScannerConfig(
    string VendorId,
    string ProductId,
    bool AutostartEnabled,
    bool VerboseLogging)
{
    public static ScannerConfig Default => new(
        VendorId: "0x0000",
        ProductId: "0x0000",
        AutostartEnabled: true,
        VerboseLogging: false);
}
