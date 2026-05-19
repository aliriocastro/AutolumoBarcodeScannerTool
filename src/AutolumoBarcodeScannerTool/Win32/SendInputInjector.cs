using System.Runtime.InteropServices;
using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Win32;

internal sealed class SendInputInjector : IInputInjector
{
    private const ushort VK_TAB = 0x09;
    private const ushort VK_RETURN = 0x0D;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    public void SendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var inputs = new List<INPUT>(capacity: text.Length * 2);
        foreach (var ch in text)
        {
            if (ch == '\t') AppendVirtualKey(inputs, VK_TAB);
            else if (ch == '\n') AppendVirtualKey(inputs, VK_RETURN);
            else if (ch == '\r') continue; // ya consumido por terminador, ignorar restantes
            else AppendUnicode(inputs, ch);
        }

        if (inputs.Count == 0) return;
        var arr = inputs.ToArray();
        _ = SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
    }

    private static void AppendVirtualKey(List<INPUT> list, ushort vk)
    {
        list.Add(MakeKey(vk, '\0', 0));
        list.Add(MakeKey(vk, '\0', KEYEVENTF_KEYUP));
    }

    private static void AppendUnicode(List<INPUT> list, char ch)
    {
        list.Add(MakeKey(0, ch, KEYEVENTF_UNICODE));
        list.Add(MakeKey(0, ch, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
    }

    private static INPUT MakeKey(ushort vk, char scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = (ushort)scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
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
}
