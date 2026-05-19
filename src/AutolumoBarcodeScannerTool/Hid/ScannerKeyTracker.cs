using System.Runtime.InteropServices;
using AutolumoBarcodeScannerTool.Diag;
using AutolumoBarcodeScannerTool.Win32;
using static AutolumoBarcodeScannerTool.Hid.RawInputInterop;

namespace AutolumoBarcodeScannerTool.Hid;

// Registra un hidden NativeWindow para recibir WM_INPUT de todos los teclados.
// El LL hook suprime preventivamente TODO VK_SPACE no marcado con nuestro
// sentinel. Esta clase es la segunda mitad del contrato: cuando llega un
// WM_INPUT de SPACE, identifica el dispositivo origen y decide:
//
//   • Si proviene del lector configurado (VID/PID match): DROP (el LL hook ya
//     lo bloqueó, no hacemos nada).
//   • Si proviene de otro dispositivo (teclado humano): SpaceInjector.SendSpace
//     re-inyecta el SPACE con sentinel, que el LL hook reconoce y deja pasar.
//
// El usuario experimenta ~5 ms de lag en sus espacios manuales — imperceptible.
internal sealed class ScannerKeyTracker : NativeWindow, IDisposable
{
    private const ushort VK_SPACE = 0x20;
    private const ushort RI_KEY_BREAK = 0x01;

    private readonly string _scannerDeviceFragment;

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

            // Sólo nos interesa keydown; el keyup también genera WM_INPUT
            // pero no tiene impacto en la re-inyección (SpaceInjector emite
            // su propio down+up).
            if ((input.Keyboard.Flags & RI_KEY_BREAK) != 0) return;

            // Nada que hacer si no es un SPACE — los demás keys nunca son
            // suprimidos por el LL hook, fluyen normalmente al foreground.
            if (input.Keyboard.VKey != VK_SPACE) return;

            var deviceName = GetDeviceName(input.Header.DeviceHandle) ?? "";
            var fromScanner = deviceName.Contains(_scannerDeviceFragment, StringComparison.OrdinalIgnoreCase);
            AppLog.Debug($"WM_INPUT VK=SPACE device='{deviceName}' fromScanner={fromScanner} action={(fromScanner ? "DROP" : "REINJECT")}");

            if (fromScanner)
            {
                // El LL hook ya lo bloqueó. No hacemos nada — el destino
                // nunca verá ese SPACE.
                return;
            }

            // Era un SPACE del teclado humano. El LL hook lo bloqueó
            // preventivamente; lo restauramos vía SendInput con sentinel.
            SpaceInjector.SendSpace();
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
