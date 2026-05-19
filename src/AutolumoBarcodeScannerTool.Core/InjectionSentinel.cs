namespace AutolumoBarcodeScannerTool.Core;

// Sentinel value stamped into KEYBDINPUT.dwExtraInfo when WE re-inject a
// keystroke via SendInput. The OS propagates dwExtraInfo verbatim into the
// KBDLLHOOKSTRUCT seen by WH_KEYBOARD_LL, so the hook can recognise our own
// injections and NOT re-suppress them (which would create an infinite drop).
//
// Value is arbitrary but unique. Other apps that call SendInput typically
// leave dwExtraInfo=0; using 0xA17 0 0C0D (Autolumo-OOO-CODE) avoids
// collisions in the wild.
public static class InjectionSentinel
{
    // new IntPtr(long) takes the value verbatim on 64-bit. A plain
    // `(IntPtr)0xA1700C0DL` cast trips a "may overflow nint at runtime"
    // compile-time warning that the CI treats as error.
    public static readonly IntPtr Value = new IntPtr(0xA1700C0DL);
}
