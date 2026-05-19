namespace AutolumoBarcodeScannerTool.Core;

public static class SpaceSuppressor
{
    private const int VK_SPACE = 0x20;

    // Returns true ⇔ the LL hook should suppress this keystroke at delivery
    // time. The decision is intentionally device-agnostic: we suppress ALL
    // VK_SPACE keystrokes EXCEPT those we ourselves re-injected via SendInput
    // (marked by InjectionSentinel.Value in dwExtraInfo).
    //
    // The WM_INPUT handler in ScannerKeyTracker arrives ~2 ms after the LL
    // hook and is the one that knows the keystroke's source device. It is
    // responsible for the second half of the contract: when a suppressed
    // SPACE turned out to come from the human keyboard (not the configured
    // scanner), it re-injects the SPACE via SendInput with the sentinel set
    // — restoring the user's keystroke with imperceptible (~5 ms) lag.
    public static bool ShouldSuppress(int vk, IntPtr dwExtraInfo)
    {
        if (vk != VK_SPACE) return false;
        if (dwExtraInfo == InjectionSentinel.Value) return false;
        return true;
    }
}
