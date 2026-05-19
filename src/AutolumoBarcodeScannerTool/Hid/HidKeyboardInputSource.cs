using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sources;
using Microsoft.Extensions.Logging;
using static AutolumoBarcodeScannerTool.Hid.RawInputInterop;

namespace AutolumoBarcodeScannerTool.Hid;

internal sealed class HidKeyboardInputSource : NativeWindow, IInputSource
{
    private readonly string _vendorIdHex;
    private readonly string _productIdHex;
    private readonly ScanTerminator _terminator;
    private readonly ILogger<HidKeyboardInputSource> _logger;
    private readonly ConcurrentQueue<(uint VKey, DateTime When, IntPtr Device)> _recentScannerKeys = new();
    private readonly System.Text.StringBuilder _payload = new();
    private LowLevelKeyboardHook? _hook;
    private SourceConnectionState _state = SourceConnectionState.Disconnected;
    private string? _scannerDeviceNameFragment;

    public event Func<ScanEvent, Task>? OnScan;
    public event EventHandler<SourceConnectionState>? StateChanged;

    public HidKeyboardInputSource(
        string vendorIdHex,
        string productIdHex,
        ScanTerminator terminator,
        ILogger<HidKeyboardInputSource> logger)
    {
        _vendorIdHex = NormalizeHex(vendorIdHex);
        _productIdHex = NormalizeHex(productIdHex);
        _terminator = terminator;
        _logger = logger;
    }

    public SourceConnectionState State => _state;

    public Task StartAsync(CancellationToken ct)
    {
        var cp = new CreateParams { Caption = "AutolumoHiddenInputWindow", X = 0, Y = 0, Width = 0, Height = 0 };
        CreateHandle(cp);

        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                UsagePage = HID_USAGE_PAGE_GENERIC,
                Usage = HID_USAGE_GENERIC_KEYBOARD,
                Flags = RIDEV_INPUTSINK,
                WindowHandle = Handle
            }
        };
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            _logger.LogError("RegisterRawInputDevices falló: {Err}", Marshal.GetLastWin32Error());
            SetState(SourceConnectionState.Error);
            return Task.CompletedTask;
        }

        _scannerDeviceNameFragment = $"VID_{_vendorIdHex}&PID_{_productIdHex}";
        _hook = new LowLevelKeyboardHook(ShouldSuppress);
        _hook.Install();
        SetState(SourceConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _hook?.Uninstall();
        _hook = null;
        if (Handle != IntPtr.Zero) DestroyHandle();

        // Clear all in-flight state so a restart begins with a clean slate.
        // Without this, repeating "Pausar"/"Reanudar" leaves stale queue entries
        // and a half-accumulated payload.
        while (_recentScannerKeys.TryDequeue(out _)) { }
        lock (_payload) _payload.Clear();

        SetState(SourceConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT) HandleRawInput(m.LParam);
        base.WndProc(ref m);
    }

    private void HandleRawInput(IntPtr hRawInput)
    {
        uint size = 0;
        _ = GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
        if (size == 0) return;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            _ = GetRawInputData(hRawInput, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            var input = Marshal.PtrToStructure<RAWINPUT>(buffer);
            if (input.Header.Type != 1) return; // RIM_TYPEKEYBOARD == 1

            var deviceName = GetDeviceName(input.Header.DeviceHandle) ?? "";
            if (!IsScannerDevice(deviceName)) return;

            // Solo nos interesa keydown
            const ushort RI_KEY_BREAK = 0x01;
            if ((input.Keyboard.Flags & RI_KEY_BREAK) != 0) return;

            _recentScannerKeys.Enqueue((input.Keyboard.VKey, DateTime.UtcNow, input.Header.DeviceHandle));
            TrimRecent();

            // Convertir VK+scan code a texto respetando el layout actual del teclado.
            // ToUnicodeEx soporta todos los caracteres alfanuméricos, símbolos (-, ., :, /, _, espacio),
            // y respeta layout (US-QWERTY, ES, etc.). Sin esto los barcodes con símbolos se truncan.
            var translated = VKeyToText(input.Keyboard.VKey, input.Keyboard.MakeCode);
            if (translated is null || translated.Length == 0) return;

            lock (_payload)
            {
                foreach (var ch in translated)
                {
                    if (ch == '\r' || ch == '\n')
                    {
                        var detected = ResolveTerminator(ch);
                        if (detected is null) continue;
                        var text = _payload.ToString();
                        _payload.Clear();
                        OnScan?.Invoke(new ScanEvent(text, detected.Value, DateTimeOffset.UtcNow));
                    }
                    else
                    {
                        _payload.Append(ch);
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private ScanTerminator? ResolveTerminator(char ch) => _terminator switch
    {
        ScanTerminator.CrLf when ch == '\n' => ScanTerminator.CrLf,
        ScanTerminator.CrLf => null, // ignorar CR sin LF en este modo
        ScanTerminator.Cr when ch == '\r' => ScanTerminator.Cr,
        ScanTerminator.Lf when ch == '\n' => ScanTerminator.Lf,
        ScanTerminator.Any when ch == '\r' => ScanTerminator.Cr,
        ScanTerminator.Any when ch == '\n' => ScanTerminator.Lf,
        _ => null
    };

    // Defense-in-depth: in addition to the WM_INPUT-confirmed queue match, track
    // burst timing. The Windows input pipeline does not guarantee WM_INPUT arrives
    // before the LL hook fires — on some kernels/versions the LL hook is first, in
    // which case the queue would be empty on lookup. Burst detection ensures
    // subsequent keys in the same rapid sequence are still suppressed even if the
    // first 1-2 leak through. We only suppress on burst when we have recent
    // scanner activity in the queue, to avoid suppressing fast human typing.
    private DateTime _lastHookKeyTime = DateTime.MinValue;
    private int _consecutiveBurstKeys;
    private const int BurstThresholdMs = 30;
    private const int BurstMinimumKeys = 2;
    private const int ScannerActivityWindowMs = 300; // any scanner key in window → in-burst

    private bool ShouldSuppress(int vkCode)
    {
        var now = DateTime.UtcNow;
        var matchCutoff = now.AddMilliseconds(-50);
        var activityCutoff = now.AddMilliseconds(-ScannerActivityWindowMs);

        var remaining = new List<(uint VKey, DateTime When, IntPtr Device)>();
        var queueMatched = false;
        var hasRecentScannerActivity = false;

        while (_recentScannerKeys.TryDequeue(out var entry))
        {
            if (entry.When >= activityCutoff)
                hasRecentScannerActivity = true;

            if (!queueMatched && entry.When >= matchCutoff && entry.VKey == vkCode)
            {
                queueMatched = true; // consume this entry — repeated chars work
                continue;
            }
            if (entry.When >= activityCutoff) remaining.Add(entry);
        }
        foreach (var e in remaining) _recentScannerKeys.Enqueue(e);

        // Update burst counter regardless, but only use it when scanner is active.
        var gap = (now - _lastHookKeyTime).TotalMilliseconds;
        if (gap < BurstThresholdMs) _consecutiveBurstKeys++;
        else _consecutiveBurstKeys = 1;
        _lastHookKeyTime = now;

        var burstSuppress = hasRecentScannerActivity && _consecutiveBurstKeys >= BurstMinimumKeys;

        return queueMatched || burstSuppress;
    }

    private void TrimRecent()
    {
        var cutoff = DateTime.UtcNow.AddMilliseconds(-200);
        while (_recentScannerKeys.TryPeek(out var first) && first.When < cutoff)
            _recentScannerKeys.TryDequeue(out _);
    }

    private bool IsScannerDevice(string deviceName) =>
        _scannerDeviceNameFragment is not null &&
        deviceName.Contains(_scannerDeviceNameFragment, StringComparison.OrdinalIgnoreCase);

    // Reusable buffers per WndProc call. WndProc runs on the UI thread so no
    // concurrency concerns.
    private readonly byte[] _keyState = new byte[256];
    private readonly System.Text.StringBuilder _toUnicodeBuf = new(8);

    private string? VKeyToText(ushort vKey, ushort scanCode)
    {
        // Special-case terminators so we never depend on layout for these.
        if (vKey == 0x0D) return "\r"; // VK_RETURN
        if (vKey == 0x09) return "\t"; // VK_TAB

        // Snapshot modifier state from the real OS keyboard at this instant.
        // GetAsyncKeyState's high bit (0x8000) indicates the key is currently
        // pressed; low bit indicates toggled (for Caps/NumLock).
        Array.Clear(_keyState);
        _keyState[0x10] = (byte)((GetAsyncKeyState(0x10) & 0x8000) != 0 ? 0x80 : 0); // VK_SHIFT
        _keyState[0x11] = (byte)((GetAsyncKeyState(0x11) & 0x8000) != 0 ? 0x80 : 0); // VK_CONTROL
        _keyState[0x12] = (byte)((GetAsyncKeyState(0x12) & 0x8000) != 0 ? 0x80 : 0); // VK_MENU (Alt)
        _keyState[0x14] = (byte)(GetAsyncKeyState(0x14) & 0x0001);                    // VK_CAPITAL toggle

        _toUnicodeBuf.Clear();
        var layout = GetKeyboardLayout(0);
        // wFlags=4 = "do not change keyboard state" — important since we don't want
        // to commit dead-key composition globally.
        int result = ToUnicodeEx(vKey, scanCode, _keyState, _toUnicodeBuf,
            _toUnicodeBuf.Capacity, 4, layout);

        if (result <= 0) return null;
        return _toUnicodeBuf.ToString(0, result);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        [Out] System.Text.StringBuilder pwszBuff, int cchBuff,
        uint wFlags, IntPtr dwhkl);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static string NormalizeHex(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        return t.ToUpperInvariant().PadLeft(4, '0');
    }

    private void SetState(SourceConnectionState s)
    {
        if (_state == s) return;
        _state = s;
        StateChanged?.Invoke(this, s);
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(StopAsync(CancellationToken.None));
    }
}
