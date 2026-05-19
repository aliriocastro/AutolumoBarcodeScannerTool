# Autolumo Barcode Scanner Tool

App Windows que intercepta el input de un lector de código de barras (Serial COM o HID-keyboard), **elimina el primer espacio** del payload y envía **ENTER** al final, antes de inyectarlo en una app destino filtrada por proceso.

Ejemplo: el lector escanea `LAB 2026-001\r\n` → la app destino recibe `LAB2026-001<ENTER>`.

El comportamiento de la salida es fijo (no configurable). Solo el modo de captura (Serial / HID), el puerto y el filtro de proceso destino se configuran.

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

Tras escanear el código de config, el lector aparece como `COM3` (o similar) en
el Administrador de Dispositivos. Configurar la app en Settings → Fuente → Serial.

**Ventaja**: el lector NO escribe en ninguna ventana directamente. La app lee
los bytes del puerto, los transforma, y solo el payload transformado se inyecta
en la app destino. Determinístico.

Para el procedimiento completo de conversión paso a paso (drivers, códigos de
configuración por marca, verificación), vea [Cómo configurar el lector de HID a
puerto Serial](#cómo-configurar-el-lector-de-hid-a-puerto-serial-paso-a-paso)
más abajo.

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

## Cómo configurar el lector de HID a puerto Serial (paso a paso)

Esta es la configuración recomendada. La mayoría de lectores comerciales modernos
soportan emisión vía **USB-CDC** (USB Communications Device Class), que hace que
Windows los exponga como un puerto COM virtual en lugar de un teclado. El cambio
es persistente en el lector — una vez aplicado, el lector recuerda el modo aunque
se desenchufe o se conecte a otra PC.

### 1. Identificar marca y modelo del lector

Busque la etiqueta en la base del lector. Necesita marca + modelo exacto
(ej. `Honeywell Voyager 1450g`, `Zebra DS2208-SR`). Con ese dato puede ubicar
el **Programming Guide / User's Guide** y el driver del puerto virtual si la
marca lo requiere.

| Marca | Sitio de manuales |
|---|---|
| Honeywell | sps.honeywell.com → Support → Technical Documents |
| Zebra / Symbol | zebra.com → Support & Downloads |
| Datalogic | datalogic.com → Support → Technical Library |
| Newland | newland-id.com → Service & Support |

### 2. Instalar el driver de puerto virtual (CDC) si la marca lo requiere

Windows 10+ trae `usbser.sys` (driver CDC genérico) y muchos lectores funcionan
sin instalar nada — al enchufar el lector ya configurado en modo Serial,
aparece un puerto COM en Device Manager. Si no aparece o aparece como
"Dispositivo desconocido", instale el driver del fabricante **antes** de volver
a conectar el lector:

| Marca | Driver típico |
|---|---|
| Honeywell | "Honeywell Scanning and Mobility USB Serial Driver" (en el portal de soporte del modelo). |
| Zebra / Symbol | "Zebra Scanner CDC Driver" o "Zebra Scanner SDK for Windows" según modelo (DS, MS, LI). |
| Datalogic | La mayoría usa `usbser.sys` nativo; algunos modelos requieren el driver USB-COM del manual. |
| Newland | Driver USB-COM dedicado en la página del modelo. |

### 3. Escanear el código de configuración "USB-CDC" / "USB Serial Emulation"

Abra el Programming Guide del lector y busque en el índice secciones con
nombres como:

- "USB Serial Emulation"
- "USB COM Port Emulation"
- "USB-CDC mode"
- "RS-232 Emulation over USB"
- "Virtual COM port"

Dentro encontrará un **código de barras de programación** que cambia el modo
del lector. Apunte el lector hacia ese código (impreso o en pantalla) y
dispare. El lector confirma con un beep.

Referencias rápidas por marca (puede variar entre modelos — siempre confirme
con su manual específico):

| Marca | Sección | Etiqueta del código |
|---|---|---|
| Honeywell (Voyager, Xenon, Granit) | "USB Serial" del Programming Guide | `USB Serial` |
| Zebra / Symbol (DS2208, DS4308, LI2208) | "USB Host Types" del Product Reference Guide | `USB CDC Host` |
| Datalogic (QuickScan, Gryphon, Heron) | "USB-COM" del Product Reference Guide | `Select USB-COM` |
| Newland (HR11, HR15, HR22) | "USB COM Port Emulation" del User Guide | `USB COM Port Emulation` |

Si su lector es genérico u OEM (sin marca reconocible), suele venir una hoja
impresa de una página con códigos para alternar entre `USB-KBW` (keyboard,
default) y `USB-COM` (serial). Escanee el `USB-COM`.

### 4. Verificar en Device Manager

1. Desconecte y vuelva a conectar el lector tras escanear el código de config.
2. Abra Device Manager (`Win+X` → "Device Manager", o ejecute `devmgmt.msc`).
3. Expanda **Ports (COM & LPT)**.
4. Debe aparecer una entrada nueva tipo:
   - `USB Serial Device (COM3)`
   - `Honeywell Scanning Virtual COM (COM4)`
   - `Zebra Symbol Scanner CDC (COM5)`

Apunte el número del COM (ej. `COM3`) — lo usará en la configuración de la app.

Si la entrada aparece bajo **Other devices** con un triángulo amarillo en lugar
de bajo "Ports (COM & LPT)", falta el driver — vuelva al paso 2 e instale el
driver del fabricante.

### 5. Probar el puerto con un terminal serial (recomendado)

Antes de configurar la app, confirme que el lector emite correctamente con
un terminal serial estándar. Esto descarta problemas de cable, driver o
configuración del lector.

Herramientas comunes (cualquiera funciona):

- **PuTTY** ([putty.org](https://www.putty.org)) — Connection type: `Serial`,
  Serial line: `COM3` (su puerto), Speed: `9600`. Click "Open".
- **RealTerm** ([realterm.sourceforge.io](https://realterm.sourceforge.io)) —
  pestaña "Port" → seleccione el COM y `9600 8N1`. Click "Open".
- **Tera Term** ([teratermproject.github.io](https://teratermproject.github.io)) —
  File → New connection → Serial → seleccione el COM.

Con el terminal abierto, escanee un código de barras. Debe ver el texto
aparecer en consola, seguido del terminador (típico `\r\n`, visible como salto
de línea). Si no aparece nada:

- ¿Suena el beep del lector al escanear?
- ¿El COM en el terminal coincide con el de Device Manager?
- ¿Hay otro proceso conectado al COM? (cierre Autolumo si estaba abierta — solo
  un proceso a la vez puede tener el puerto abierto.)

**Importante**: cierre el terminal (libere el puerto) antes de iniciar la app
Autolumo.

### 6. Parámetros típicos del puerto

Los defaults de fábrica de la mayoría de lectores son:

| Parámetro | Valor típico |
|---|---|
| Baud rate | `9600` |
| Data bits | `8` |
| Parity | `None` |
| Stop bits | `One` |
| Flow control | `None` |
| Encoding | `Ascii` |
| Terminator | `CrLf` (`\r\n`) |

El Programming Guide incluye códigos para cambiar baud (`115200`, `57600`, …)
y terminador (CR-only, LF-only, TAB, …) si su flujo lo requiere. Para casos
normales, deje los defaults.

### 7. Configurar la app

1. Click derecho en el ícono de bandeja → **Configuración...**.
2. Pestaña **General**:
   - "Tipo de fuente" → `Serial`.
   - "Terminador" → `CrLf` (o el que su lector emita).
3. Pestaña **Serial**:
   - **Puerto**: seleccione el COM detectado en el paso 4.
   - **Baud rate**: `9600` (o el que configuró en el lector).
   - **Parity** / **Stop bits**: `None` / `One`.
   - **Encoding**: `Ascii` (`Latin1` si su código contiene acentos).
4. Pestaña **Destino**: nombre de proceso de la app que recibe los códigos
   (sin `.exe`, exacto, case-insensitive).
5. Click **Guardar**.
6. **Cierre la app desde el menú de bandeja (Salir) y vuelva a abrirla** —
   los cambios de puerto, baud, encoding y terminador requieren reinicio del
   proceso (solo el "Nombre de proceso" destino es hot-reload).

### 8. Volver a HID-keyboard (rollback)

Si necesita revertir el lector a su modo de teclado original (default de
fábrica), busque en el mismo Programming Guide el código:

- "USB Keyboard" / "USB HID Keyboard" / "USB-KBW"

Escanéelo y el lector vuelve a aparecer como teclado HID en Windows.

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
