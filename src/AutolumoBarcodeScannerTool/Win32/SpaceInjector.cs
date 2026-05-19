using System.Runtime.InteropServices;
using AutolumoBarcodeScannerTool.Core;

namespace AutolumoBarcodeScannerTool.Win32;

// Re-inyecta un único VK_SPACE marcado con InjectionSentinel.Value en
// dwExtraInfo. El WH_KEYBOARD_LL lo identificará como nuestro y dejará pasar.
//
// Sólo necesitamos SPACE — los demás keys nunca se suprimen, no hace falta
// reinyectarlos.
internal static class SpaceInjector
{
    private const ushort VK_SPACE = 0x20;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void SendSpace()
    {
        var inputs = new[]
        {
            MakeKey(KEYEVENTF_KEYUP: 0),
            MakeKey(KEYEVENTF_KEYUP: KEYEVENTF_KEYUP)
        };
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKey(uint KEYEVENTF_KEYUP) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = VK_SPACE,
                wScan = 0,
                dwFlags = KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = InjectionSentinel.Value
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // CRITICAL: INPUT union must include MOUSEINPUT (32 bytes) so the overall
    // struct size matches Win32: 4 (type) + 4 (padding x64) + 32 (union) = 40 on x64.
    // Without all three union variants, Marshal.SizeOf<INPUT>() returns the wrong
    // size and SendInput silently fails (returns 0, GetLastError=ERROR_INVALID_PARAMETER).
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}
