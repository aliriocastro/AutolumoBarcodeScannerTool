namespace AutolumoBarcodeScannerTool.Core;

public static class SpaceSuppressor
{
    private const int VK_SPACE = 0x20;
    private const int ScannerWindowMs = 100;

    // Returns true ⇔ the keystroke should be suppressed at the LL hook layer.
    // Suppress only when ALL of:
    //   • vk is VK_SPACE
    //   • the configured scanner produced a keystroke within ScannerWindowMs
    //   • the foreground window matches the configured target
    // If both filter strings are empty, the target check passes (suppress applies
    // wherever the focus is — least-surprise default).
    //
    // `foreground` is a factory, not a value, so the (potentially expensive)
    // Win32 + Process.GetProcessById call only runs for keys that already
    // passed the cheap vk-and-time guards. The LL hook fires on every keydown
    // in the system; non-space keys must return in microseconds.
    public static bool ShouldSuppress(
        int vk,
        DateTime now,
        DateTime lastScannerKey,
        Func<ForegroundWindowInfo?> foreground,
        string targetProcessName,
        string targetWindowTitleContains)
    {
        if (vk != VK_SPACE) return false;
        if ((now - lastScannerKey).TotalMilliseconds >= ScannerWindowMs) return false;

        var fg = foreground();
        if (fg is null) return false;

        if (!string.IsNullOrEmpty(targetProcessName) &&
            !string.Equals(fg.ProcessName, targetProcessName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(targetWindowTitleContains) &&
            fg.WindowTitle.IndexOf(targetWindowTitleContains, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return true;
    }
}
