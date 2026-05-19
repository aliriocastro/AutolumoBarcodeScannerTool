using System.Runtime.InteropServices;
using System.Threading;
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
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly string _scannerDeviceFragment;
    private long _wmInputCount;

    public ScannerKeyTracker(string vendorIdHex, string productIdHex)
    {
        _scannerDeviceFragment = $"VID_{NormalizeHex(vendorIdHex)}&PID_{NormalizeHex(productIdHex)}";
    }

    public void Start()
    {
        // Message-only window (HWND_MESSAGE parent) — diseñada explícitamente
        // para recibir mensajes sin ser visible. Una NativeWindow overlapped
        // top-level invisible NO recibe WM_INPUT de forma fiable en .NET 10
        // (síntoma observado en v0.2.3: LL hook dispara pero WM_INPUT nunca
        // llega al WndProc, así que la re-inyección nunca se intenta).
        var cp = new CreateParams
        {
            Caption = "AutolumoScannerKeyTracker",
            Parent = HWND_MESSAGE
        };
        CreateHandle(cp);
        AppLog.Info($"ScannerKeyTracker.Handle=0x{Handle.ToInt64():X} (message-only)");

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
        var ok = RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        var err = Marshal.GetLastWin32Error();
        AppLog.Info($"RegisterRawInputDevices ok={ok} lastError={err} fragment={_scannerDeviceFragment}");
        if (!ok)
            throw new InvalidOperationException($"RegisterRawInputDevices falló: {err}");
    }

    public void Stop()
    {
        if (Handle != IntPtr.Zero) DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT)
        {
            var n = Interlocked.Increment(ref _wmInputCount);
            // Log de heartbeat — sólo el primer mensaje + cada 50 después,
            // para confirmar que el pipeline funciona sin saturar el log.
            if (n == 1 || n % 50 == 0) AppLog.Debug($"WM_INPUT recibidos hasta ahora: {n}");
            HandleRawInput(m.LParam);
        }
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

            // Eventos sintéticos de SendInput tienen DeviceHandle = 0 y no
            // resuelven device name. Si no los filtramos aquí, nuestra propia
            // re-inyección dispara WM_INPUT → "no es scanner" → re-inyectamos
            // de nuevo → loop infinito hasta que Windows tira el hook.
            if (input.Header.DeviceHandle == IntPtr.Zero)
            {
                AppLog.Debug("WM_INPUT VK=SPACE synthetic (hDevice=0) — skipping");
                return;
            }
            var deviceName = GetDeviceName(input.Header.DeviceHandle) ?? "";
            if (string.IsNullOrEmpty(deviceName))
            {
                AppLog.Debug("WM_INPUT VK=SPACE empty device name — skipping (likely synthetic)");
                return;
            }

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
            var sent = SpaceInjector.SendSpace();
            AppLog.Debug($"SpaceInjector.SendSpace → sent={sent} (esperado 2)");
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
