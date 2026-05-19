namespace AutolumoBarcodeScannerTool.Core;

public static class ConfigIo
{
    private const string KeyVendor = "VendorId";
    private const string KeyProduct = "ProductId";
    private const string KeyAutostart = "AutostartEnabled";
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
            AutostartEnabled = ParseBoolOrDefault(values, KeyAutostart, d.AutostartEnabled),
            VerboseLogging = ParseBoolOrDefault(values, KeyVerbose, d.VerboseLogging)
        };
    }

    // Malformed bool values fall back to default rather than silently mapping
    // to false — a hand-edited file shouldn't flip a default-true setting.
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
            $"{KeyAutostart}={(config.AutostartEnabled ? "true" : "false")}",
            $"{KeyVerbose}={(config.VerboseLogging ? "true" : "false")}"
        };
        File.WriteAllLines(path, lines);
    }
}
