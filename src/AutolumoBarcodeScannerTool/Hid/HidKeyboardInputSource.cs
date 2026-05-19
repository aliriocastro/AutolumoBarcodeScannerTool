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

            // Acumular char en payload
            var ch = MapVKeyToChar(input.Keyboard.VKey);
            if (ch is null) return;

            lock (_payload)
            {
                if (ch == '\r' || ch == '\n')
                {
                    var detected = ResolveTerminator(ch.Value);
                    if (detected is null) return;
                    var text = _payload.ToString();
                    _payload.Clear();
                    OnScan?.Invoke(new ScanEvent(text, detected.Value, DateTimeOffset.UtcNow));
                }
                else
                {
                    _payload.Append(ch.Value);
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

    private bool ShouldSuppress(int vkCode)
    {
        var cutoff = DateTime.UtcNow.AddMilliseconds(-50);
        foreach (var (vk, when, _) in _recentScannerKeys)
        {
            if (when >= cutoff && vk == vkCode) return true;
        }
        return false;
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

    private static char? MapVKeyToChar(ushort vKey)
    {
        // Mapeo minimal para barcode payloads (alfanumérico + comunes)
        // Para producción se usaría ToUnicode con keyboard layout; aquí simplificado.
        if (vKey >= 0x30 && vKey <= 0x39) return (char)vKey; // 0-9
        if (vKey >= 0x41 && vKey <= 0x5A) return (char)vKey; // A-Z
        if (vKey == 0x0D) return '\r';
        if (vKey == 0x09) return '\t';
        return null;
    }

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
