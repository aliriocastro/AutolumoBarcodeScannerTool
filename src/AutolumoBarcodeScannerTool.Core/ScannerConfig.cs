namespace AutolumoBarcodeScannerTool.Core;

public sealed record ScannerConfig(
    string VendorId,
    string ProductId,
    string TargetProcessName,
    string TargetWindowTitleContains,
    bool AutostartEnabled)
{
    public static ScannerConfig Default => new(
        VendorId: "0x0000",
        ProductId: "0x0000",
        TargetProcessName: "",
        TargetWindowTitleContains: "",
        AutostartEnabled: true);
}
