namespace AutolumoBarcodeScannerTool.Core;

public static class ConfigIo
{
    private const string KeyVendor = "VendorId";
    private const string KeyProduct = "ProductId";
    private const string KeyProcess = "TargetProcessName";
    private const string KeyTitle = "TargetWindowTitleContains";
    private const string KeyAutostart = "AutostartEnabled";

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
            if (line.StartsWith('[') && line.EndsWith(']')) continue; // section header — ignored

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
            AutostartEnabled = values.TryGetValue(KeyAutostart, out var a)
                ? bool.TryParse(a, out var ab) && ab
                : d.AutostartEnabled
        };
    }

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
            $"{KeyAutostart}={(config.AutostartEnabled ? "true" : "false")}"
        };
        File.WriteAllLines(path, lines);
    }
}
