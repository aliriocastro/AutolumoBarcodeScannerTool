namespace AutolumoBarcodeScannerTool.Core.Configuration;

public static class IniConfigWriter
{
    public static string Update(string original, string fullKey, string value)
    {
        var (sectionPath, keyName) = SplitKey(fullKey);
        var lines = original.ReplaceLineEndings("\n").Split('\n').ToList();

        var sectionRange = FindSectionRange(lines, sectionPath);

        if (sectionRange is null)
        {
            // Section doesn't exist: append a blank line, the header, and the key.
            if (lines.Count > 0 && !string.IsNullOrEmpty(lines[^1]))
                lines.Add(string.Empty);
            lines.Add($"[{sectionPath}]");
            lines.Add($"{keyName}={value}");
            return string.Join("\n", lines);
        }

        var (start, end) = sectionRange.Value;
        for (var i = start + 1; i <= end; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith(';') || trimmed.StartsWith('#') || string.IsNullOrWhiteSpace(trimmed))
                continue;

            var eq = lines[i].IndexOf('=');
            if (eq < 0) continue;

            var currentKey = lines[i][..eq].Trim();
            if (string.Equals(currentKey, keyName, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{keyName}={value}";
                return string.Join("\n", lines);
            }
        }

        // Key not found inside the section: append at end of section.
        lines.Insert(end + 1, $"{keyName}={value}");
        return string.Join("\n", lines);
    }

    private static (string SectionPath, string KeyName) SplitKey(string fullKey)
    {
        var lastColon = fullKey.LastIndexOf(':');
        if (lastColon < 0)
            throw new ArgumentException($"Key must include section: '{fullKey}'", nameof(fullKey));
        return (fullKey[..lastColon], fullKey[(lastColon + 1)..]);
    }

    private static (int Start, int End)? FindSectionRange(List<string> lines, string sectionPath)
    {
        var header = $"[{sectionPath}]";
        var start = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), header, StringComparison.OrdinalIgnoreCase))
            {
                start = i;
                break;
            }
        }
        if (start < 0) return null;

        var end = lines.Count - 1;
        for (var j = start + 1; j < lines.Count; j++)
        {
            var t = lines[j].TrimStart();
            if (t.StartsWith('[') && t.EndsWith(']'))
            {
                end = j - 1;
                break;
            }
        }
        return (start, end);
    }
}
