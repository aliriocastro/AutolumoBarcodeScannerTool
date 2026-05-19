namespace AutolumoBarcodeScannerTool.Core;

public static class ConfigIo
{
    private const string KeyVendor = "VendorId";
    private const string KeyProduct = "ProductId";
    private const string KeyProcess = "TargetProcessName";
    private const string KeyTitle = "TargetWindowTitleContains";
    private const string KeyAutostart = "AutostartEnabled";
    private const string KeyIgnoreWindow = "IgnoreWindowFilter";
    private const string KeyVerbose = "VerboseLogging";

    public static ScannerConfig Load(string path)
    {
        if (!File.Exists(path)) return ScannerConfig.Default;

        var d = ScannerConfig.Default;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.TrimStart();
            if (line.Length == 0) continue;
            if (line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) continue;

            var eq = line.IndexOf('=');
            if (eq < 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            values[key] = value;
        }

        return d with
        {
            VendorId = values.TryGetValue(KeyVendor, out var v) ? v : d.VendorId,
            ProductId = values.TryGetValue(KeyProduct, out var p) ? p : d.ProductId,
            TargetProcessName = values.TryGetValue(KeyProcess, out var pr) ? pr : d.TargetProcessName,
            TargetWindowTitleContains = values.TryGetValue(KeyTitle, out var t) ? t : d.TargetWindowTitleContains,
            AutostartEnabled = ParseBoolOrDefault(values, KeyAutostart, d.AutostartEnabled),
            IgnoreWindowFilter = ParseBoolOrDefault(values, KeyIgnoreWindow, d.IgnoreWindowFilter),
            VerboseLogging = ParseBoolOrDefault(values, KeyVerbose, d.VerboseLogging)
        };
    }

    // Malformed bool values (e.g. "yes", "1") fall back to default rather than
    // silently mapping to false — a hand-edited file shouldn't flip a default-true
    // setting to false without saying so.
    private static bool ParseBoolOrDefault(Dictionary<string, string> values, string key, bool dflt) =>
        values.TryGetValue(key, out var a) && bool.TryParse(a, out var ab) ? ab : dflt;

    public static void Save(string path, ScannerConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var lines = new[]
        {
            $"{KeyVendor}={config.VendorId}",
            $"{KeyProduct}={config.ProductId}",
            $"{KeyProcess}={config.TargetProcessName}",
            $"{KeyTitle}={config.TargetWindowTitleContains}",
            $"{KeyAutostart}={(config.AutostartEnabled ? "true" : "false")}",
            $"{KeyIgnoreWindow}={(config.IgnoreWindowFilter ? "true" : "false")}",
            $"{KeyVerbose}={(config.VerboseLogging ? "true" : "false")}"
        };
        File.WriteAllLines(path, lines);
    }
}
