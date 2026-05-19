# AutolumoBarcodeScannerTool — Design Spec

- **Fecha:** 2026-05-18
- **Estado:** Aprobado para implementación
- **Carpeta del proyecto:** `/Users/aliriocastro/Labotech/AutobioKeylogger` (nombre histórico, se conserva)
- **Nombre de producto / binario:** `AutolumoBarcodeScannerTool`

---

## 1. Contexto y problema

Una aplicación destino interna ("MiAppContable" como placeholder de configuración) presenta un bug: cuando recibe el secuencia `CRLF` en el input, **borra todo el contenido del campo**. Los lectores de código de barras conectados a las estaciones envían `CRLF` como terminador por defecto al final de cada escaneo, lo cual rompe el flujo de trabajo del operador.

La solución es un **bridge en software** que:

1. Intercepte el input proveniente exclusivamente del lector configurado (sin afectar el teclado del operador).
2. Reemplace el terminador `CRLF` por `TAB` (u otra secuencia configurable) antes de entregarlo a la app destino.
3. Solo entregue el input transformado si la ventana activa pertenece a la app destino.

## 2. Objetivos y no-objetivos

### Objetivos

- Soportar lectores conectados como **puerto serial/COM** (incluyendo USB-CDC) y como **HID-keyboard USB**.
- Configuración dinámica vía UI accesible desde el ícono de bandeja del sistema.
- Autoarranque al iniciar sesión del usuario en Windows 10 y posteriores.
- Funcionar como aplicación de bandeja (tray) en español.
- Distribución como instalador `.exe` autoinstalable.

### No-objetivos (fuera de alcance de v1)

- Soporte multi-usuario en la misma máquina (cada usuario configura su propia instancia, pero no se diseña para concurrencia).
- Soporte para múltiples lectores simultáneos.
- Soporte para Windows Server, Linux, macOS, o arquitecturas distintas a x64.
- Driver kernel (Interception) como alternativa al hook user-mode.
- Code-signing del binario (recomendado para producción, planeado fuera de v1).
- Internacionalización (i18n) de la UI.

## 3. Stack técnico

| Componente | Elección | Razón |
|---|---|---|
| Runtime | .NET 8 LTS | Soporte largo, mejor performance que .NET Framework |
| Target Framework | `net8.0-windows` | Acceso a Win32 APIs sin esfuerzo |
| UI Toolkit | WinForms (`NotifyIcon`, Forms) | Path más simple para tray apps; WPF requeriría wrappers |
| Configuración | `Microsoft.Extensions.Configuration` + `IOptionsMonitor<T>` | Hot-reload de config sin reiniciar la app |
| Host | `Microsoft.Extensions.Hosting` (Generic Host) | DI, lifetime management, IHostedService |
| Logging | Serilog (sink: File con rolling diario) | Logs estructurados, rotación nativa |
| Tests | xUnit + FluentAssertions | Estándar del ecosistema |
| Instalador | Inno Setup 6 | Gratis, open-source, script `.iss` simple |
| Publish | `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` | Un solo `.exe`, sin requisito de .NET instalado |

**Plataforma mínima:** Windows 10 versión 1809 (build 17763) o superior, x64.

## 4. Arquitectura

### Diagrama de componentes

```
┌──────────────────────────────────────────────────────────────────┐
│                  Tray UI (WinForms NotifyIcon)                   │
│  ▸ Estado: Activo / Pausado / Desconectado / Error              │
│  ▸ Menú contextual: Pausar/Reanudar · Configuración · Ver logs · Salir │
└──────────────────────────┬───────────────────────────────────────┘
                           │  comandos UI
┌──────────────────────────▼───────────────────────────────────────┐
│              ScannerOrchestrator (IHostedService)                │
│  Cablea Source → Transform pipeline → Sink                       │
│  Escucha IOptionsMonitor para reaccionar a cambios de config     │
└──┬──────────────────────┬──────────────────────────────┬─────────┘
   │ ScanEvent            │ ScanEvent transformado       │
┌──▼─────────────────┐ ┌──▼─────────────────────┐ ┌──────▼──────────┐
│  IInputSource      │ │  ITerminatorTransform  │ │  IInputSink     │
├────────────────────┤ ├────────────────────────┤ ├─────────────────┤
│ SerialInputSource  │ │ ReplaceTerminator      │ │ Foreground      │
│ HidKeyboardSource  │ │  Transform             │ │  ProcessSink    │
└────────────────────┘ └────────────────────────┘ └─────────────────┘
```

### Flujo de datos

1. **Source** recibe bytes/keystrokes del lector y acumula en un buffer interno.
2. Al detectar el **terminador configurado** (`CR`, `LF`, `CRLF`, o `Any`), emite un `ScanEvent { Payload, DetectedTerminator, Timestamp }`. El payload **no incluye** el terminador.
3. **Transform pipeline** procesa el evento. En v1 solo hay un transform: `ReplaceTerminatorTransform` que adjunta el sufijo configurado (`Tab` / `TabEnter` / `TabOnly` / `Custom`). Los `CRLF` **internos** del payload no se tocan.
4. **Sink** recibe el `ScanEvent` listo, valida que la ventana foreground pertenezca al proceso objetivo, e inyecta vía `SendInput` (con flag `KEYEVENTF_UNICODE` para soportar caracteres no-ASCII). Si no coincide → descarta + log.

### Concurrencia

- El sink usa un `SemaphoreSlim(1, 1)` para serializar inyecciones.
- Si llega un segundo escaneo mientras el sink está ocupado → `WaitAsync(TimeSpan.Zero)` falla y el escaneo se **descarta con log warning**.

## 5. Configuración

### Ubicación

`%LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.json`

(El instalador crea el archivo con valores por defecto si no existe en el primer arranque.)

### Esquema

```jsonc
{
  "Scanner": {
    "Enabled": true,
    "SourceType": "Serial",            // "Serial" | "HidKeyboard"
    "Terminator": "CRLF",              // "CR" | "LF" | "CRLF" | "Any"

    "Serial": {
      "PortName": "COM3",
      "BaudRate": 9600,
      "DataBits": 8,
      "Parity": "None",                // "None" | "Even" | "Odd" | "Mark" | "Space"
      "StopBits": "One",               // "One" | "OnePointFive" | "Two"
      "Encoding": "ASCII"              // "ASCII" | "UTF8" | "Latin1"
    },
    "HidKeyboard": {
      "VendorId": "0x05E0",            // hex string
      "ProductId": "0x1300"
    },

    "Target": {
      "ProcessName": "MiAppContable",  // sin .exe
      "WindowTitleContains": null      // opcional, null = solo proceso
    },

    "Output": {
      "OnTerminator": "Tab",           // "Tab" | "TabEnter" | "TabOnly" | "Custom"
      "OutputSuffix": "{TAB}"          // tokens: {TAB} {ENTER} — usado si OnTerminator=Custom
    }
  },
  "Autostart": true,
  "Logging": {
    "MinimumLevel": "Information",     // Verbose | Debug | Information | Warning | Error
    "LogPayload": true                 // false en entornos con datos sensibles
  }
}
```

### Hot-reload

`IOptionsMonitor<ScannerOptions>.OnChange` dispara `ScannerOrchestrator.RestartAsync()`, que cierra el source actual, recarga config y reinicia con los nuevos valores. No requiere reiniciar la app.

### UI de configuración

Ventana modal WinForms abierta desde el menú contextual del tray, organizada en pestañas:

- **General:** habilitar/deshabilitar, autoarranque al login, idioma (read-only "Español").
- **Fuente:** dropdown SourceType. Si Serial → lista de puertos COM detectados (`SerialPort.GetPortNames()`) + baud, parity, etc. Si HidKeyboard → lista de dispositivos HID enumerados (con descripción del dispositivo) + VID/PID derivados.
- **Destino:** dropdown con procesos en ejecución + input manual para nombre de proceso. Campo opcional "WindowTitleContains".
- **Salida:** radio buttons para `Tab` / `TabEnter` / `TabOnly` / `Custom`. Si `Custom` → campo de texto con sufijo.
- **Logs:** path al folder de logs (botón "Abrir"), checkbox `LogPayload`, dropdown de nivel.

Al guardar → escribe `appsettings.json`. El `IOptionsMonitor` toma el cambio.

## 6. Componentes en detalle

### 6.1 `IInputSource`

```csharp
public interface IInputSource : IAsyncDisposable
{
    event Func<ScanEvent, Task>? OnScan;
    Task StartAsync(CancellationToken ct);
    SourceConnectionState State { get; }
    event EventHandler<SourceConnectionState>? StateChanged;
}

public sealed record ScanEvent(string Payload, ScanTerminator DetectedTerminator, DateTimeOffset Timestamp);

public enum ScanTerminator { Cr, Lf, CrLf }
public enum SourceConnectionState { Disconnected, Connecting, Connected, Error }
```

#### `SerialInputSource`

- Wrap de `System.IO.Ports.SerialPort`.
- Open con los parámetros configurados; si falla → `State = Error`, retry exponencial cada 5s/10s/30s (cap 30s).
- `DataReceived` handler lee bytes, los decodifica con el encoding configurado, los acumula en `StringBuilder`.
- Cuando aparece el terminador configurado → emite `ScanEvent` con el payload sin terminador.
- Edge case: si `Terminator=CRLF` y solo llega `CR`, espera hasta 100ms más por el `LF`. Si no aparece, emite `ScanEvent` con terminador `Cr`.

#### `HidKeyboardInputSource`

- Crea ventana oculta para recibir `WM_INPUT`.
- `RegisterRawInputDevices` para `HID_USAGE_GENERIC_KEYBOARD` con flag `RIDEV_INPUTSINK` (recibe input incluso sin focus).
- `SetWindowsHookEx(WH_KEYBOARD_LL)` para hook global.
- **Identificación de dispositivo:** al iniciar, enumera dispositivos HID conectados, busca por `VendorId`/`ProductId` configurados, guarda el `RAWINPUTDEVICELIST.hDevice` correspondiente.
- **Lógica del hook:**
  - Mantiene buffer ring de últimos 32 eventos `(vkCode, timestamp, hDevice)` provenientes de `WM_INPUT`.
  - Cuando el hook se dispara con un `vkCode`, busca el evento WM_INPUT más reciente con el mismo `vkCode` en los últimos 50ms.
  - Si su `hDevice` coincide con el lector configurado → swallow (retorna `1`) + acumula el char en buffer del source.
  - Si no coincide o no hay match → `CallNextHookEx` (pasa).
- Al detectar el terminador en el acumulador → emite `ScanEvent`.
- Reconexión: suscribe `WM_DEVICECHANGE`; si llega `DBT_DEVNODES_CHANGED`, re-enumera HID y rebusca el dispositivo.

### 6.2 `ITerminatorTransform`

```csharp
public interface ITerminatorTransform
{
    ScanEvent Apply(ScanEvent input);
}

public sealed class ReplaceTerminatorTransform : ITerminatorTransform
{
    public ScanEvent Apply(ScanEvent input) =>
        input with { Payload = input.Payload + ResolveSuffix() };

    private string ResolveSuffix() => _opts.OnTerminator switch
    {
        OutputMode.Tab => "\t",
        OutputMode.TabEnter => "\t\n",
        OutputMode.TabOnly => "",
        OutputMode.Custom => _opts.OutputSuffix
            .Replace("{TAB}", "\t").Replace("{ENTER}", "\n"),
        _ => "\t"
    };
}
```

### 6.3 `IInputSink`

```csharp
public interface IInputSink
{
    Task SendAsync(ScanEvent ev, CancellationToken ct);
}

public sealed class ForegroundProcessSink : IInputSink
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task SendAsync(ScanEvent ev, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct))
        {
            _logger.LogWarning("Escaneo descartado: sink ocupado con escaneo previo");
            return;
        }
        try
        {
            var (procName, title) = GetForegroundWindowInfo();
            if (!Matches(procName, title))
            {
                _logger.LogInformation("Escaneo descartado: ventana activa '{Proc}' no coincide con target '{Target}'",
                    procName, _opts.Target.ProcessName);
                return;
            }
            SendInputMixed(ev.Payload);
        }
        finally { _gate.Release(); }
    }
}
```

**Manejo de teclas especiales:** El sink itera el payload carácter por carácter:

- `\t` (`0x09`) → envía `VK_TAB` como virtual key (`KEYEVENTF_KEYDOWN`/`KEYEVENTF_KEYUP` sin `KEYEVENTF_UNICODE`).
- `\n` (`0x0A`) → envía `VK_RETURN`.
- Resto de caracteres → envía como Unicode con `KEYEVENTF_UNICODE` (un `INPUT` por char).

Razón: `KEYEVENTF_UNICODE` con `\n` no dispara correctamente el evento "Enter" en muchos formularios — la app destino espera `VK_RETURN` real. Lo mismo para Tab y la navegación entre campos.

**Implicación de diseño:** si el payload **escaneado** contiene un `\t` o `\n` interno (raro pero posible en QR con datos tabulados), también se interpretará como Tab/Enter al inyectar. Comportamiento aceptado y documentado.

**P/Invokes requeridos:**

| API | Uso |
|---|---|
| `user32.GetForegroundWindow` | Identificar ventana activa |
| `user32.GetWindowThreadProcessId` | Obtener PID de la ventana activa |
| `user32.GetWindowText` | Leer título si `WindowTitleContains` está configurado |
| `user32.SendInput` | Inyectar keystrokes con `KEYEVENTF_UNICODE` |
| `user32.SetWindowsHookEx` / `CallNextHookEx` / `UnhookWindowsHookEx` | LL keyboard hook |
| `user32.RegisterRawInputDevices` / `GetRawInputData` / `GetRawInputDeviceList` | Identificación per-device |
| `setupapi.SetupDiGetClassDevs` + `hid.HidD_GetAttributes` | Enumeración HID con VID/PID |

## 7. Manejo de errores y estados

| Situación | Comportamiento |
|---|---|
| Puerto COM no disponible o lector desconectado | Source entra en `Disconnected` → ícono tray rojo → retry exponencial → al reconectar, ícono verde |
| Dispositivo HID no encontrado por VID/PID | `Error` → log + notificación toast → revisa al recibir `WM_DEVICECHANGE` |
| App destino no abierta cuando llega escaneo | Descarta + log info, sin notificación al usuario (ruidoso) |
| Excepción en transform o sink | Catch en orchestrator → log error con stack → app sigue corriendo |
| Excepción al escribir `appsettings.json` | Notificación toast + log error, mantiene valores en memoria |
| Permisos insuficientes para HKCU\Run | Notificación toast explicando que el autostart falló |

## 8. Autoarranque

- Implementado en `AutostartManager.cs`.
- Clave: `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run\AutolumoBarcodeScannerTool`.
- Valor: `"%PROGRAMFILES%\Autolumo\BarcodeScannerTool\AutolumoBarcodeScannerTool.exe" --minimized`.
- Toggle en UI Settings escribe o borra la clave.
- El binario, al arrancar con `--minimized`, no muestra ventana Settings, solo el ícono en bandeja.

## 9. Logging

- **Library:** Serilog (`Serilog.Sinks.File` con rolling diario).
- **Path:** `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\logs\app-YYYYMMDD.log`.
- **Retención:** 30 archivos (configurable).
- **Formato:** `[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}`.
- **Categorías:**
  - `INFO` — start/stop, conexión/desconexión del lector, escaneos exitosos (con payload si `LogPayload=true`), escaneos descartados por foreground no-target.
  - `WARN` — escaneos descartados por sink ocupado, reconexiones tras timeout, autostart falló.
  - `ERROR` — excepciones en source/transform/sink, fallos de Win32 APIs.

## 10. Naming y consideraciones de antivirus

El binario, ensamblado y metadata se llaman explícitamente `AutolumoBarcodeScannerTool` y describen claramente su función:

| Propiedad | Valor |
|---|---|
| AssemblyName | `AutolumoBarcodeScannerTool` |
| AssemblyTitle | `Autolumo Barcode Scanner Tool` |
| AssemblyDescription | `Bridge para integración de lectores de código de barras con software contable Labotech` |
| AssemblyCompany | `Labotech` |
| AssemblyProduct | `Autolumo Barcode Scanner Tool` |

**Riesgo conocido:** la combinación (hook global de teclado + autoarranque + manipulación de input) seguirá levantando heurísticas en AV agresivos (Windows Defender, Kaspersky, Bitdefender corporativo). Mitigación recomendada para producción (fuera de alcance v1):

- Code-signing con certificado EV o estándar (~$200-$400/año).
- Whitelist explícita en consolas de AV corporativos.
- Documentación clara del binary hash y comportamiento esperado para administradores de TI.

## 11. Estrategia de testing

### Unit tests (xUnit + FluentAssertions)

- `ReplaceTerminatorTransform`: cubre los 4 modos (`Tab`, `TabEnter`, `TabOnly`, `Custom` con tokens).
- Parsing de terminadores en `SerialInputSource` con `FakeSerialPort` (interfaz wrapper inyectable).
- Matching de proceso/título en `ForegroundProcessSink` con interfaz `IForegroundWindowProvider` mockeable.

### Integration tests

- `FakeInputSource` emite secuencias sintéticas (con CR, LF, CRLF, encoding latin1, payloads multilínea) → verifica que el pipeline completo (transform + sink) produce el output esperado.
- `FakeInputSink` captura los outputs para asserts.

### Pruebas manuales documentadas (`docs/manual-test-checklist.md`)

Casos que requieren hardware real:

- [ ] Lector Honeywell Voyager en modo Serial (USB-CDC, COM auto-asignado).
- [ ] Lector Zebra DS2208 en modo HID-keyboard.
- [ ] Verificar que tecleo manual del operador en el campo de la app destino NO es intercepted.
- [ ] Verificar que escaneo con app destino minimizada se descarta (no se acumula).
- [ ] Verificar reconexión: desenchufar/enchufar el lector durante operación.
- [ ] Verificar autoarranque tras reboot.
- [ ] Verificar comportamiento al cerrar/abrir la app destino mientras la app está corriendo.

## 12. Estructura de carpetas

```
AutobioKeylogger/                              (carpeta histórica)
├── docs/
│   ├── superpowers/specs/
│   │   └── 2026-05-18-autolumo-barcode-scanner-tool-design.md  (este archivo)
│   └── manual-test-checklist.md
├── installer/
│   └── AutolumoBarcodeScannerTool.iss         (Inno Setup script)
├── src/
│   └── AutolumoBarcodeScannerTool/
│       ├── AutolumoBarcodeScannerTool.csproj  (net8.0-windows, WinExe)
│       ├── Program.cs
│       ├── Tray/
│       │   ├── TrayIconController.cs
│       │   ├── SettingsForm.cs (+ Designer)
│       │   └── TrayIcons/ (.ico assets)
│       ├── Core/
│       │   ├── ScannerOrchestrator.cs
│       │   ├── ScannerOptions.cs
│       │   └── Models/
│       │       ├── ScanEvent.cs
│       │       └── Enums.cs
│       ├── Sources/
│       │   ├── IInputSource.cs
│       │   ├── Serial/
│       │   │   └── SerialInputSource.cs
│       │   └── Hid/
│       │       ├── HidKeyboardInputSource.cs
│       │       ├── RawInputInterop.cs
│       │       ├── LowLevelKeyboardHook.cs
│       │       └── HidDeviceEnumerator.cs
│       ├── Transforms/
│       │   ├── ITerminatorTransform.cs
│       │   └── ReplaceTerminatorTransform.cs
│       ├── Sinks/
│       │   ├── IInputSink.cs
│       │   ├── ForegroundProcessSink.cs
│       │   ├── SendInputInterop.cs
│       │   └── IForegroundWindowProvider.cs
│       ├── Autostart/
│       │   └── AutostartManager.cs
│       └── appsettings.default.json           (template instalable)
├── tests/
│   └── AutolumoBarcodeScannerTool.Tests/
│       ├── AutolumoBarcodeScannerTool.Tests.csproj
│       ├── Transforms/
│       ├── Sources/
│       │   └── Fakes/ (FakeSerialPort, etc.)
│       └── Sinks/
│           └── Fakes/ (FakeInputSink, FakeForegroundWindowProvider)
├── AutolumoBarcodeScannerTool.sln
└── README.md
```

## 13. Decisiones a confirmar antes de release

- **Code-signing:** decidir si se adquiere certificado antes de producción.
- **Política de retención de logs:** actualmente 30 días — confirmar con compliance/legal si los payloads escaneados son datos sensibles.
- **Soporte para múltiples perfiles de app destino:** v1 soporta una sola app destino; si en el futuro se necesita rotar entre varias, agregar `Profiles[]` en config.
- **Distribución del instalador:** confirmar canal (web, GPO, USB, otro) — esto afecta firma y empaque.
