# Autolumo Barcode Scanner Tool

App Windows que intercepta el input de un lector de código de barras (Serial COM o HID-keyboard) y reemplaza el terminador `CRLF` por `TAB` (configurable) antes de inyectarlo en una app destino filtrada por proceso.

## Requisitos

- Windows 10 (build 17763) o superior, x64
- Permisos de usuario estándar (no requiere admin)
- .NET 10 SDK para compilar

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
