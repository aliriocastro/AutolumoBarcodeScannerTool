using System.Runtime.InteropServices;
using static AutolumoBarcodeScannerTool.Hid.RawInputInterop;

namespace AutolumoBarcodeScannerTool.Hid;

// Registers a Raw Input hidden window for keyboard devices. Every WM_INPUT
// whose source device matches "VID_xxxx&PID_yyyy" stamps the current time
// into LastScannerKeyTime. The LL hook reads that value to decide whether a
// VK_SPACE belongs to a scanner burst or to the human keyboard.
internal sealed class ScannerKeyTracker : NativeWindow, IDisposable
{
    private readonly string _scannerDeviceFragment;
    public DateTime LastScannerKeyTime { get; private set; } = DateTime.MinValue;

    public ScannerKeyTracker(string vendorIdHex, string productIdHex)
    {
        _scannerDeviceFragment = $"VID_{NormalizeHex(vendorIdHex)}&PID_{NormalizeHex(productIdHex)}";
    }

    public void Start()
    {
        var cp = new CreateParams { Caption = "AutolumoScannerKeyTracker", X = 0, Y = 0, Width = 0, Height = 0 };
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
            throw new InvalidOperationException(
                $"RegisterRawInputDevices falló: {Marshal.GetLastWin32Error()}");
    }

    public void Stop()
    {
        if (Handle != IntPtr.Zero) DestroyHandle();
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

            // Solo keydown — keyup también dispara WM_INPUT pero no nos importa
            // marcar la ventana de actividad para el keyup.
            const ushort RI_KEY_BREAK = 0x01;
            if ((input.Keyboard.Flags & RI_KEY_BREAK) != 0) return;

            var deviceName = GetDeviceName(input.Header.DeviceHandle) ?? "";
            if (!deviceName.Contains(_scannerDeviceFragment, StringComparison.OrdinalIgnoreCase)) return;

            LastScannerKeyTime = DateTime.UtcNow;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string NormalizeHex(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        return t.ToUpperInvariant().PadLeft(4, '0');
    }

    public void Dispose() => Stop();
}
