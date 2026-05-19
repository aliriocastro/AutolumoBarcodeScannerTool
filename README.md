# Autolumo Barcode Scanner Tool

App Windows que intercepta el input de un lector de código de barras (Serial COM o HID-keyboard) y reemplaza el terminador `CRLF` por `TAB` (configurable) antes de inyectarlo en una app destino filtrada por proceso.

## Requisitos

- Windows 10 (build 17763) o superior, x64
- Permisos de usuario estándar (no requiere admin)
- .NET 10 SDK para compilar

## Modos de operación — IMPORTANTE

La app soporta dos modos de captura del lector:

### ✅ Modo Serial (RECOMENDADO)

El lector se configura para emitir vía puerto serial / USB-CDC en lugar de teclado.
La mayoría de lectores comerciales (Honeywell, Zebra/Symbol, Datalogic, Newland) lo
soportan vía un **código de barras de configuración** que viene en su manual.

Buscar en el manual del lector secciones como:
- "USB Serial Emulation" / "USB COM Port Emulation"
- "USB-CDC mode"
- "RS-232 emulation"

Tras escanear el código de config, el lector aparece como `COM3` (o similar) en
el Administrador de Dispositivos. Configurar la app en Settings → Fuente → Serial.

**Ventaja**: el lector NO escribe en ninguna ventana directamente. La app lee
los bytes del puerto, los transforma, y solo el payload transformado se inyecta
en la app destino. Determinístico.

### ⚠ Modo HID-keyboard (best-effort)

El lector queda en su modo por defecto (teclado USB). Windows lo trata como un
teclado más. La app intercepta vía Raw Input + Low-Level Hook.

**Limitación conocida**: la supresión del input crudo del lector hacia la app
destino es **heurística (timing-based)**. En la práctica, los primeros 1-2
caracteres de cada escaneo pueden filtrarse al destino antes que la heurística
detecte la ráfaga. Para escaneo infrecuente sin tecleo concurrente esto suele
ser aceptable, pero **no es determinístico**.

**Recomendación**: si su lector permite modo Serial, úselo. HID-keyboard mode
debe considerarse fallback.

## Build (en Windows)

```cmd
dotnet publish src\AutolumoBarcodeScannerTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

El binario queda en `src\AutolumoBarcodeScannerTool\bin\Release\net10.0-windows\win-x64\publish\AutolumoBarcodeScannerTool.exe`.

## Build del instalador (en Windows)

Requiere [Inno Setup 6](https://jrsoftware.org/isdl.php).

```cmd
ISCC.exe installer\AutolumoBarcodeScannerTool.iss
```

El instalador queda en `artifacts\installer\`.

## Tests (cualquier plataforma)

```bash
dotnet test
```

## Configuración

Tras instalar, abrir `Configuración` desde el ícono de bandeja. La configuración se guarda en:

```
%LOCALAPPDATA%\AutolumoBarcodeScannerTool\appsettings.ini
```

Los logs en:

```
%LOCALAPPDATA%\AutolumoBarcodeScannerTool\logs\app-YYYYMMDD.log
```

## Estructura

- `src/AutolumoBarcodeScannerTool.Core` — lógica cross-platform (modelos, transforms, source serial, sink, config writer)
- `src/AutolumoBarcodeScannerTool` — WinForms tray, Win32 P/Invokes, HID, autostart
- `tests/AutolumoBarcodeScannerTool.Tests` — xUnit + Shouldly
- `installer/` — script Inno Setup
- `docs/` — specs, plans, checklist manual

## Licencia

Propietaria — Labotech.
