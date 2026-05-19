namespace AutolumoBarcodeScannerTool.Hid;

// Sentinel para distinguir keystrokes inyectadas por nuestro SendInputInjector
// vs keystrokes reales del lector HID en el LL hook.
//
// SendInput propaga el campo KEYBDINPUT.dwExtraInfo al campo
// KBDLLHOOKSTRUCT.dwExtraInfo que recibe el WH_KEYBOARD_LL. Si ponemos
// este valor en cada key que inyectamos, el hook puede reconocerlas y
// NO suprimirlas — de lo contrario nuestro propio output se filtra
// porque la heurística de burst del HidKeyboardInputSource confunde
// nuestras teclas con las del lector.
//
// El valor es arbitrario pero único; cualquier otra app que llame
// SendInput con dwExtraInfo=0 (lo normal) o con su propio sentinel no
// va a colisionar.
internal static class InjectionMarker
{
    // new IntPtr(long) takes the value verbatim on 64-bit (lo only on 32-bit,
    // which we don't ship — RuntimeIdentifier is win-x64). Using a plain
    // `(IntPtr)0xA1700C0DL` cast trips a "may overflow nint at runtime"
    // compile-time warning that the CI treats as error.
    public static readonly IntPtr Sentinel = new IntPtr(0xA1700C0DL);
}
