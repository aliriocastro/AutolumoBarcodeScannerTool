namespace AutolumoBarcodeScannerTool.Core;

public static class SpaceSuppressor
{
    private const int VK_SPACE = 0x20;
    private const uint LLKHF_INJECTED = 0x10;

    // Returns true ⇔ the LL hook should suppress this keystroke at delivery
    // time. The decision is intentionally device-agnostic: we suppress ALL
    // VK_SPACE keystrokes EXCEPT those marked as injected (our own SendInput
    // re-injections from the human keyboard).
    //
    // Two redundant signals identify "this is our own re-injection":
    //   • flags & LLKHF_INJECTED — set by Windows for any SendInput event.
    //     Reliable across input pipeline paths.
    //   • dwExtraInfo == InjectionSentinel.Value — our explicit marker.
    //     Kept as belt-and-suspenders in case future Windows changes filter
    //     the injected flag for hooks.
    //
    // Either signal is sufficient to pass. The scanner is a USB HID keyboard
    // emulator — its keystrokes go through the physical input path and have
    // neither signal set.
    public static bool ShouldSuppress(int vk, uint flags, IntPtr dwExtraInfo)
    {
        if (vk != VK_SPACE) return false;
        if ((flags & LLKHF_INJECTED) != 0) return false;
        if (dwExtraInfo == InjectionSentinel.Value) return false;
        return true;
    }
}
