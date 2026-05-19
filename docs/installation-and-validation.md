# Manual de instalación y validación — Autolumo Barcode Scanner Tool v0.1.1

Este documento te guía paso a paso desde un Windows limpio hasta confirmar
que la app funciona contra tu hardware y tu app destino. Síguelo en orden;
no saltes pasos. Si algo falla, ve a la sección **Troubleshooting** al final.

---

## 0. Pre-requisitos

- Windows 10 build 17763 (1809) o superior, x64
- Cuenta de usuario estándar (no requiere admin)
- El lector de código de barras a usar
- La app destino instalada y abierta (la que recibe los códigos)

**Recomendación fuerte sobre el lector:** configúralo en modo **Serial /
USB-CDC** antes de empezar. Consulta el manual del lector — busca el código
de configuración "USB Serial Emulation" o "USB-CDC". Una vez aplicado, el
lector aparece como `COM3` (o similar) en el Administrador de Dispositivos.
Si por alguna razón no es posible, podemos usar modo HID-keyboard pero es
best-effort (ver README sección "Modos de operación").

---

## 1. Descarga e instalación

### 1a. Descargar el release

Ve a https://github.com/aliriocastro/AutolumoBarcodeScannerTool/releases/tag/v0.1.1
y descarga `AutolumoBarcodeScannerTool-0.1.1-win-x64.zip`.

### 1b. Extraer

Crea una carpeta permanente para la app, por ejemplo:
```
C:\Program Files\Autolumo\BarcodeScannerTool\
```
(o `%LOCALAPPDATA%\Programs\AutolumoBarcodeScannerTool\` si no tienes permiso
sobre `Program Files`).

Extrae el contenido del zip ahí. Debes ver:
- `AutolumoBarcodeScannerTool.exe` (~112 MB)
- `appsettings.default.ini`
- 2 archivos `.pdb` (debug symbols — se pueden borrar para ahorrar espacio,
  pero ayudan al diagnóstico si hay crash)

### 1c. Primera ejecución

Doble-click en `AutolumoBarcodeScannerTool.exe`.

**Si aparece SmartScreen** ("Windows protected your PC"):
- Click en "More info"
- Click en "Run anyway"
- (Esto es esperado porque el binario no está firmado. Para producción
  conviene code-signing, pero está fuera de alcance de v0.1.x.)

**Si Windows Defender alerta**: agrega una excepción para el .exe — un
keylogger-style tool que se autoarranca es bandera roja por diseño, aunque
el comportamiento es legítimo. Si tu organización tiene AV corporativo
puede que necesite whitelist explícito.

### 1d. Verificar que arrancó

Mira la bandeja del sistema (esquina inferior derecha, posiblemente bajo el
icono `^`). Debes ver el ícono de Autolumo (icono genérico de Windows en
v0.1.1; los íconos custom están diferidos a v0.2).

**Si NO ves el ícono:**
- Verifica `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\logs\app-YYYYMMDD.log`.
  Abre con Notepad. Busca lines `[ERR]` o `[FTL]`.
- Si no hay log, el proceso falló antes de inicializar Serilog. Revisa el
  Visor de Eventos de Windows (Event Viewer → Windows Logs → Application).

---

## 2. Configuración inicial

### 2a. Abrir Settings

Click derecho en el ícono de bandeja → **Configuración...**

### 2b. Pestaña "General"

- **Servicio habilitado**: marcado (es lo default)
- **Autoarranque al iniciar sesión**: marca SI quieres que la app inicie
  con Windows. Esto crea entrada en `HKCU\Software\Microsoft\Windows\
  CurrentVersion\Run`.
- **Tipo de fuente**: elige `Serial` (recomendado) o `HidKeyboard`
- **Terminador**: `CrLf` es lo más común; ajusta según tu lector

### 2c. Pestaña "Serial" (si elegiste Serial)

- **Puerto**: dropdown muestra los puertos COM detectados. Selecciona el
  que corresponde al lector. Si no estás seguro, abre Administrador de
  Dispositivos → Ports (COM & LPT) y mira cuál aparece al enchufar/
  desenchufar el lector.
- **Baud rate**: típicamente `9600` (consulta manual del lector)
- **Parity / Stop bits**: típicamente `None` / `One`
- **Encoding**: `Ascii` cubre la mayoría de casos. `Latin1` si tu lector
  emite caracteres con acentos. `Utf8` si confirmas que el lector emite
  UTF-8 (raro).

### 2d. Pestaña "HID" (si elegiste HidKeyboard)

- **Dispositivo detectado**: dropdown con todos los HID. Selecciona el
  lector. Esto auto-llena VID/PID.
- Si tu lector no aparece, enchúfalo, cierra Settings, ábrela de nuevo.
- VID/PID son 4 hex chars, con o sin prefijo `0x`.

### 2e. Pestaña "Destino"

- **Nombre de proceso**: el nombre EXACTO del proceso, **SIN .exe**.
  Por ejemplo si tu app es `MiAppContable.exe`, escribe `MiAppContable`.
  Es case-insensitive pero el nombre debe ser exacto.
  - Para verificar: Task Manager → Details tab → mira la columna "Name".
- **Contiene en título** (opcional): substring que debe estar en el título
  de la ventana. Útil si tu app destino tiene varias ventanas y solo
  quieres inyectar en la de "Factura nueva". Dejar vacío para no filtrar.

### 2f. Pestaña "Salida"

- **Modo al terminador**:
  - `Tab` — payload + TAB (lo más común para navegar al siguiente campo)
  - `TabEnter` — payload + TAB + ENTER (si la app también necesita Enter
    para confirmar)
  - `TabOnly` — payload sin sufijo (raro)
  - `Custom` — usa el sufijo del campo siguiente

- **Sufijo (Custom)**: solo aplica si `Custom`. Tokens: `{TAB}`, `{ENTER}`.
  Ejemplo: `{TAB}END{ENTER}` envía payload + TAB + literal "END" + ENTER.

### 2g. Guardar

Click "Guardar". Aparece un MessageBox confirmando qué cambios son
hot-reload y cuáles requieren reinicio.

**Cambios hot-reload (toman efecto inmediatamente):**
- Proceso destino
- Contiene en título
- Modo de salida y sufijo

**Cambios que requieren reinicio (cierra app y vuelve a abrir):**
- Tipo de fuente (Serial ↔ HidKeyboard)
- Puerto COM, baud rate, parity, stop bits, encoding
- VID/PID del HID
- Terminador
- Servicio habilitado (toma efecto, pero el flag inicial de la próxima
  ejecución se lee al arranque)

---

## 3. Checklist de validación

Cada test tiene un **paso** (lo que hago) y un **resultado esperado** (lo
que debe pasar). Marca cada uno.

### Test 1 — La app está activa

- [ ] Paso: nada (solo observar)
- Esperado: ícono visible en bandeja del sistema. Click derecho muestra
  menú con "Pausar / Configuración... / Ver logs / Salir".

### Test 2 — Configuración persistió

- [ ] Paso: abre Settings, modifica algo no crítico (ej. agregar texto en
  "Contiene en título"). Guarda. Cierra Settings. Vuelve a abrir.
- Esperado: el cambio persiste.
- Verifica también: abre `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\
  appsettings.ini` con Notepad y confirma que el cambio está reflejado.

### Test 3 — Escaneo básico llega a la app destino

- [ ] Paso 1: abre tu app destino. Pon el cursor en el campo donde
  esperas que llegue el código (ej. campo "código de producto").
- [ ] Paso 2: escanea un código de prueba conocido (ej. un código que
  tengas a mano).
- Esperado:
  - El payload aparece en el campo
  - Termina con un TAB (cursor se mueve al siguiente campo) o lo que
    hayas configurado en "Modo al terminador"
  - NO ves CRLF / saltos de línea / contenido borrado
  - Si tu app destino antes "se borraba" con CRLF, ahora NO se borra

### Test 4 — Caracteres especiales en barcode (CRÍTICO para lab/medical)

Solo aplica si tu uso tiene barcodes con `-`, `.`, `:`, espacios, `_`, `/`.

- [ ] Paso: escanea un código que contenga al menos uno de esos caracteres.
  Si no tienes a mano, pide al admin que genere uno (online: https://
  barcode.tec-it.com/en/Code128?data=LAB-2026-05-001)
- Esperado: el payload llega COMPLETO al destino, incluyendo todos los
  símbolos. Ej. `LAB-2026-05-001` no se trunca a `LAB20260519001`.

### Test 5 — Filtro por proceso destino

- [ ] Paso 1: cambia la ventana activa a OTRA app (ej. Notepad, Calc)
- [ ] Paso 2: escanea
- Esperado: NADA se escribe en la app no-destino. El escaneo es descartado.
- [ ] Paso 3: revisa el log más reciente. Debe haber una línea
  `[INF] AutolumoBarcodeScannerTool.Core.Sinks.ForegroundProcessSink:
  Escaneo descartado: ventana activa 'notepad' no coincide con target...`

### Test 6 — Vuelve a la app destino

- [ ] Paso: trae la app destino al frente. Cursor en el campo correcto.
  Escanea.
- Esperado: payload llega. (Verifica que el filtro permite cuando el
  destino está activo.)

### Test 7 — Pausa/Reanudar desde tray

- [ ] Paso 1: click derecho ícono → "Pausar". Ícono cambia o muestra estado.
- [ ] Paso 2: escanea
- Esperado: NADA en el destino, ni en otra parte.
- [ ] Paso 3: click derecho → "Reanudar". Ícono vuelve a activo.
- [ ] Paso 4: escanea
- Esperado: payload llega al destino.

### Test 8 — Hot-reload del proceso destino

- [ ] Paso 1: abre Settings. Cambia "Nombre de proceso" a algo INCORRECTO
  (ej. `notexistingproc`). Guarda. Cierra.
- [ ] Paso 2: con la app destino al frente, escanea.
- Esperado: NADA llega al destino. Log muestra "no coincide con target
  'notexistingproc'".
- [ ] Paso 3: vuelve a Settings. Pon el proceso correcto. Guarda.
- [ ] Paso 4: escanea.
- Esperado: payload llega correctamente. (Esto confirma hot-reload del sink.)

### Test 9 — Autostart al reiniciar sesión

Solo aplica si activaste autostart en Settings.

- [ ] Paso 1: ejecuta `regedit`. Verifica que existe la entrada
  `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run\
  AutolumoBarcodeScannerTool` apuntando a tu .exe + ` --minimized`.
- [ ] Paso 2: cierra sesión de Windows (no apagar, solo logout) y vuelve
  a iniciar sesión.
- Esperado: ícono de Autolumo aparece en bandeja del sistema unos
  segundos después del login.

### Test 10 — Doble instancia bloqueada

- [ ] Paso: con la app ya corriendo (ícono en bandeja), doble-click al
  .exe de nuevo.
- Esperado: aparece MessageBox "Autolumo Barcode Scanner Tool ya está
  en ejecución. Revisa el ícono en la bandeja del sistema." y la
  segunda instancia se cierra.

### Test 11 — Logs accesibles

- [ ] Paso: click derecho ícono → "Ver logs"
- Esperado: se abre Windows Explorer en
  `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\logs\`. Hay al menos un
  archivo `app-YYYYMMDD.log`. Abrirlo muestra líneas de tiempo con
  formato `[HH:mm:ss.fff LVL]`.

### Test 12 — Salida limpia

- [ ] Paso: click derecho → "Salir"
- Esperado: ícono desaparece de la bandeja. Task Manager confirma que
  `AutolumoBarcodeScannerTool.exe` ya no está corriendo.
- Verifica el log: las últimas líneas no deben tener excepciones.

---

## 4. Troubleshooting

### Síntoma: el ícono no aparece en bandeja

**Causas posibles:**

1. **El proceso crashea al arrancar**. Mira el último `app-*.log`:
   - Si hay `[FTL] Fallo fatal en arranque` con stack trace → reportar.
   - Si no hay log nada, el crash es antes de Serilog. Ver Event Viewer.

2. **Otra instancia ya está corriendo**. Ver Test 10. Mira Task Manager.

3. **Antivirus bloquea silenciosamente**. Verifica logs de tu AV.

### Síntoma: escaneo no llega al destino (Serial mode)

**Pasos de diagnóstico:**

1. Verifica que el puerto COM correcto está configurado. Cierra la app,
   abre PuTTY o RealTerm en ese COM con los mismos parámetros (baud,
   parity, etc.). Escanea. Si los bytes NO aparecen en PuTTY, el problema
   es entre el lector y el puerto (no es nuestra app).

2. Si los bytes SÍ aparecen en PuTTY, cierra PuTTY (suelta el puerto),
   abre la app y mira el log. Debes ver `[INF] ... SerialInputSource:
   ... Connected`. Si dice `Error` o `Disconnected`, hay un problema
   con el SerialPort (probablemente el puerto está ocupado por PuTTY o
   un driver).

3. Si Connected pero igual no llega: revisa el "Nombre de proceso" en
   Settings. Debe coincidir EXACTAMENTE con el ProcessName del Task
   Manager (sin .exe).

4. Revisa el log para líneas "Escaneo descartado: ventana activa 'X'
   no coincide con target 'Y'" — te dice exactamente por qué descarta.

### Síntoma: escaneo no llega al destino (HID mode)

1. Verifica VID/PID. Abre Administrador de Dispositivos → vista
   "Devices by connection" → busca el lector → Properties → Details →
   "Hardware Ids". Verás algo como `HID\VID_05E0&PID_1300&...`. Esos
   son los valores que deben estar en Settings.

2. Recuerda que HID mode es best-effort para la supresión. El lector
   PUEDE estar typeando su input crudo en la app destino además de
   nuestro payload transformado. Para corroborar: ve a Notepad,
   escanea con Notepad enfocado. Si aparece el raw barcode + CRLF
   en Notepad, la supresión no está deteniéndolo y verás el
   comportamiento doble en tu app real.

3. La solución robusta es reconfigurar el lector a modo Serial.

### Síntoma: caracteres del barcode se truncan

Si esto pasa en HID mode con un símbolo específico, es porque
`ToUnicodeEx` con el layout actual no traduce ese vKey. Reporta:

- El layout del teclado de Windows (Settings → Time & Language →
  Language → Preferred languages → tu idioma → Options → Keyboards)
- El barcode exacto que falla
- Lo que llega vs lo esperado

### Síntoma: lector se desconecta (cable jostled) y app se queda en Error

En v0.1.1 NO hay reconexión automática del Serial. Cuando esto pase:

1. Click derecho ícono → Pausar
2. Espera 2 segundos
3. Click derecho ícono → Reanudar

Esto reinicializa el `SerialPort.Open()`. Si el cable está conectado,
debería reconectar.

### Síntoma: Settings guardó pero los cambios no se aplican

- Cambios al **proceso destino** y **título** se aplican inmediatamente
  (hot-reload via `IOptionsMonitor`).
- TODOS los demás (SourceType, port name, baud, VID/PID, terminator,
  encoding, output mode/suffix) requieren reiniciar la app.

Cierra desde tray (Salir) y vuelve a abrir.

### Síntoma: hot-reload se "pega" tras editar el .ini a mano

Edita siempre vía Settings UI. Si editas el .ini con Notepad y guardas,
algunos editores escriben de forma rara (atomic-rename, doble write)
que puede confundir al `FileSystemWatcher` de .NET. Si pasa, reinicia
la app.

---

## 5. Información a recolectar si reportas un bug

Para que el siguiente fix sea preciso, recolecta:

1. **Versión del binario**: `AutolumoBarcodeScannerTool.exe` properties → Details → Product version (debería ser `0.1.1`)
2. **Sistema operativo**: `winver` en cmd → screenshot de la ventana de versión
3. **Logs**: el `app-YYYYMMDD.log` completo del día del incidente,
   ubicado en `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\logs\`
4. **Configuración**: el archivo `appsettings.ini` (asegúrate que no
   contiene info sensible antes de compartirlo)
5. **Descripción**:
   - Qué hiciste (paso por paso)
   - Qué esperabas
   - Qué pasó realmente
   - Si es reproducible: cuántas veces y bajo qué condiciones

Crea un issue en https://github.com/aliriocastro/AutolumoBarcodeScannerTool/issues
con todo lo anterior.

---

## 6. Resumen para deploy en producción

Una vez que TODOS los tests del capítulo 3 pasan en tu máquina de pruebas:

1. Copia el `.exe` a la máquina de producción.
2. Mismo procedimiento de instalación (capítulo 1).
3. Configuración igual a la validada (puedes copiar el `appsettings.ini`
   probado a `%LOCALAPPDATA%\AutolumoBarcodeScannerTool\` antes del
   primer arranque).
4. Activa autostart desde Settings.
5. Reinicia para confirmar que arranca con Windows.

---

## Apéndice — qué hace la app paso por paso

Para depurar es útil entender el flujo:

```
Lector escanea código
   │
   ▼
[Modo Serial]                     [Modo HID-keyboard]
   │                                  │
   ▼                                  ▼
SerialPort.DataReceived            Raw Input + LL Hook
fires con bytes                    fires con vKey codes
   │                                  │
   ▼                                  ▼
SerialInputSource buffer           HidKeyboardInputSource
acumula hasta CRLF                 acumula hasta CR (con
                                   supresión heurística)
   │                                  │
   └──────────────┬───────────────────┘
                  ▼
       ScanEvent { Payload, Terminator, Timestamp }
                  │
                  ▼
       ScannerOrchestrator.HandleScanAsync
                  │
                  ▼
       ReplaceTerminatorTransform aplica sufijo
       (Tab / TabEnter / TabOnly / Custom)
                  │
                  ▼
       ForegroundProcessSink:
       1. WaitAsync(0) en semáforo → si ocupado, descarta
       2. GetForegroundWindow + GetWindowThreadProcessId
       3. Compara process name con TargetOptions
       4. Si match, SendInput unicode + virtual keys
       5. Release semáforo
                  │
                  ▼
       App destino recibe input vía SendInput
```

Cada bloque loguea entradas y salidas a Serilog. Para depurar, sigue
el log desde "Connected" hasta "Sent" o "Escaneo descartado".
