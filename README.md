# Autolumo Barcode Scanner Tool

App de bandeja para un único equipo Windows que **suprime los espacios** generados por un lector de códigos de barra HID-keyboard, dejando intacto el resto de las teclas (las del lector y las del teclado humano).

## Cómo funciona

El lector dispara teclas a >100 ch/s; el teclado humano teclea a 5-10 ch/s. La app combina dos señales para decidir qué espacios suprimir:

1. **Origen del dispositivo:** un `WM_INPUT` Raw Input registrado para el VID/PID del lector marca cada keystroke con un timestamp.
2. **Ventana activa:** la app sólo actúa si el foreground coincide con el proceso/título configurado.

El callback de `WH_KEYBOARD_LL` suprime un `VK_SPACE` ⇔ los dos chequeos pasan dentro de los últimos 100 ms.

Resultado: cuando el lector escanea `"PREFIX MIDDLE\r"` con la ventana objetivo al frente, la app destino recibe `"PREFIXMIDDLE\r"`. Cuando el operador teclea `"Hola mundo"` con esa misma ventana al frente, recibe `"Hola mundo"` intacto.

## Configuración (`Configurar…` desde la bandeja)

- **Dispositivo lector:** ComboBox con todos los HID-keyboards detectados.
- **Ventana objetivo:** nombre de proceso (sin `.exe`) y/o substring del título. Si ambos vacíos, suprime espacios del lector en cualquier app.
- **Iniciar con Windows:** registra/desregistra en `HKCU\…\Run`.

El config persiste en `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.ini` (texto plano, `clave=valor`, 5 líneas). Para aplicar cambios reiniciar la app desde la bandeja.

## Requisitos

- Windows 10 (build 17763) o superior, x64
- **.NET 10 Desktop Runtime (x64)** — descarga: https://dotnet.microsoft.com/download/dotnet/10.0/runtime (elegir "Desktop Runtime")
- Permisos de usuario estándar (no requiere admin, salvo registrar autostart la primera vez si lo activas)

## Instalación

1. Descarga `AutolumoBarcodeScannerTool-<version>-win-x64-fxdep.zip` del último release.
2. Extrae a una carpeta persistente (ej. `C:\Tools\Autolumo`).
3. Ejecuta `AutolumoBarcodeScannerTool.exe`. Aparece un ícono en la bandeja.
4. Click derecho → **Configurar…** → elige tu lector → guardar → reinicia la app.

No requiere instalador, no escribe en `Program Files`.

## Limitaciones conocidas

- Si tu lector arranca el código con un espacio Y el `WH_KEYBOARD_LL` se invoca antes que el `WM_INPUT` correspondiente, ese primer espacio escapa al destino (~5% de los casos según la implementación de Windows). Si lo ves frecuentemente, edita `SpaceSuppressor.ScannerWindowMs` (de 100 a 200) y recompila.
- Si el operador teclea un espacio JUSTO después de escanear (<100 ms en la misma ventana objetivo), ese espacio se suprime falsamente. Físicamente improbable.

## Desarrollo

- .NET 10. Tests cross-platform en `AutolumoBarcodeScannerTool.Core` (pure logic, 19 tests). Build del exe requiere Windows.
- `dotnet test tests/AutolumoBarcodeScannerTool.Tests/AutolumoBarcodeScannerTool.Tests.csproj` corre los tests (funciona en macOS/Linux).
- Para release: `git tag vX.Y.Z && git push --tags` dispara CI, que publica un único zip framework-dependent en el release de GitHub.

## Arquitectura

```
src/AutolumoBarcodeScannerTool.Core/        (net10.0 — pure logic)
  SpaceSuppressor.cs                        decisión: ¿suprimir esta tecla?
  ScannerConfig.cs                          record persistido
  ConfigIo.cs                               load/save .ini key=value
  ForegroundWindowInfo.cs                   record { ProcessName, WindowTitle }

src/AutolumoBarcodeScannerTool/             (net10.0-windows — WinForms exe)
  Program.cs                                entry + tray + wiring
  Hid/LowLevelKeyboardHook.cs               WH_KEYBOARD_LL
  Hid/ScannerKeyTracker.cs                  WM_INPUT → LastScannerKeyTime
  Hid/RawInputInterop.cs                    P/Invokes Raw Input
  Hid/HidDeviceEnumerator.cs                lista dispositivos para el ComboBox
  Win32/ForegroundWindow.cs                 GetForegroundWindow + GetWindowText
  Tray/TrayController.cs                    NotifyIcon
  Tray/SettingsForm.cs                      UI de una pestaña
  Autostart/AutostartManager.cs             HKCU\…\Run
```

## Historial

- **v0.2.0** — refactor radical. La app deja de capturar + transformar + reinyectar (pipeline Source/Transform/Sink + SendInput). Ahora sólo suprime `VK_SPACE` cuando hay scan activo + ventana objetivo. Sin DI, sin Hosting, sin Serilog, sin Serial. ~700 LOC totales.
- **v0.1.x** — pipeline completo con HID Raw Input + ToUnicodeEx + transform + SendInput re-inyección, IOptionsMonitor hot-reload, Serilog rolling logs, diagnostics verbose, soporte Serial COM.
