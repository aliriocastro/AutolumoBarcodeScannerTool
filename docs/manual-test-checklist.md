# Checklist de pruebas manuales

Pruebas que requieren hardware real (lector de código de barras) o validación en Windows.
Marcar cada caso con la fecha y el responsable.

## Modo Serial

- [ ] Lector configurado en modo Serial (USB-CDC) reconocido como COM auto-asignado
- [ ] Escanear un código alfanumérico → llega al campo destino con `TAB` al final, no `CRLF`
- [ ] Desconectar el lector → ícono de bandeja cambia a rojo, log registra `Disconnected`
- [ ] Reconectar el lector → ícono vuelve a verde, escaneo siguiente funciona
- [ ] Cambiar `Encoding` a `Latin1` en config → escanear código con `ñ` se transfiere correctamente

## Modo HID-keyboard

- [ ] Lector configurado en modo HID-keyboard, identificado por VID/PID en Settings
- [ ] Escanear → solo el resultado transformado llega a la app destino (las teclas crudas son suprimidas)
- [ ] Tipear manualmente en el campo destino → el tecleo manual NO es suprimido
- [ ] Desenchufar y reenchufar el lector → reconexión automática

## Foreground

- [ ] App destino al frente → escaneo entrega input
- [ ] App destino minimizada / otra app al frente → escaneo descartado + log info
- [ ] Cambiar `WindowTitleContains` y abrir ventana con título distinto → escaneo descartado
- [ ] Cambiar ventana al título correcto → escaneo entrega input

## Concurrencia

- [ ] Disparar dos escaneos en menos de 200ms → el segundo se descarta y se loguea warning

## Autostart

- [ ] Activar autoarranque en Settings → reiniciar Windows → app aparece en bandeja al login
- [ ] Desactivar autoarranque → reiniciar → app NO aparece

## Hot-reload

- [ ] Editar `appsettings.ini` y cambiar `Scanner:Target:ProcessName` → próximo escaneo respeta el nuevo target sin reiniciar app

## Antivirus

- [ ] Windows Defender no marca el binario tras 24h en uso normal
- [ ] (Si aplica) AV corporativo permite el binario (whitelist o no levanta alerta)
