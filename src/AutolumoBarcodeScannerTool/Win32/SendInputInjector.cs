using System.Runtime.InteropServices;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sinks;
using AutolumoBarcodeScannerTool.Hid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutolumoBarcodeScannerTool.Win32;

internal sealed class SendInputInjector : IInputInjector
{
    private const ushort VK_TAB = 0x09;
    private const ushort VK_RETURN = 0x0D;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private readonly IOptionsMonitor<DiagnosticsOptions> _diag;
    private readonly ILogger<SendInputInjector> _logger;

    public SendInputInjector(
        IOptionsMonitor<DiagnosticsOptions> diag,
        ILogger<SendInputInjector> logger)
    {
        _diag = diag;
        _logger = logger;
    }

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
        var injected = SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
        if (_diag.CurrentValue.VerboseLogging)
        {
            var err = injected == arr.Length ? 0 : Marshal.GetLastWin32Error();
            _logger.LogInformation(
                "SendInput sent={Sent}/{Total} text='{Text}' (len={Len}) lastError={Err}",
                injected, arr.Length,
                text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"),
                text.Length, err);
        }
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
                // Sentinel for the WH_KEYBOARD_LL hook in HID mode — without it
                // our own keystrokes get suppressed by the source's burst
                // heuristic, since SendInput happens right after the scanner
                // burst within the "recent scanner activity" window.
                dwExtraInfo = InjectionMarker.Sentinel
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
