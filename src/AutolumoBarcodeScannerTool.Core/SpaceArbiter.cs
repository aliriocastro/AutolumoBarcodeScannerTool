namespace AutolumoBarcodeScannerTool.Core;

// Decide si un SPACE suprimido por el LL hook debe ser restaurado (era humano)
// o quedar bloqueado (era parte de un burst del lector).
//
// CONTRATO:
//   • LL hook llama OnSpaceSuppressed cada vez que suprime un SPACE
//     físico (no sentinel, no LLKHF_INJECTED).
//   • LL hook llama OnNonSpaceKey con cualquier otra tecla física.
//   • Si dentro de _delayMs no llega ninguna otra tecla → restauramos el
//     SPACE vía la callback restoreSpace (que llama SendInput con sentinel).
//   • Si dentro de _delayMs llega cualquier otra tecla → cancelamos la
//     restauración: el SPACE era parte de un burst del lector.
//
// ¿POR QUÉ TIMING Y NO POR DISPOSITIVO?
//   El enfoque "LL hook bloquea + WM_INPUT identifica device" no es posible
//   en user-mode: cuando el LL hook retorna 1 para suprimir, Windows descarta
//   el evento upstream de Raw Input, así que WM_INPUT nunca llega y no
//   podemos saber quién lo envió. Timing es un heurístico robusto porque los
//   scanners HID típicos emiten a 100-300 chars/s (≤10 ms entre chars) y los
//   humanos a 50-200 ms entre chars. Un umbral en 25 ms separa ambos sin
//   falsos positivos prácticos.
public sealed class SpaceArbiter
{
    private readonly Action _restoreSpace;
    private readonly int _delayMs;
    private readonly object _lock = new();
    private CancellationTokenSource? _pending;

    public SpaceArbiter(Action restoreSpace, int delayMs = 25)
    {
        _restoreSpace = restoreSpace;
        _delayMs = delayMs;
    }

    public void OnSpaceSuppressed()
    {
        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            // Si ya había un SPACE pendiente, lo reemplazamos. Esto causa que
            // dos SPACEs humanos consecutivos rápidos colapsen en uno, pero
            // ese caso es poco común en datos normales y prefe­rible a una
            // implementación con lista de pendientes.
            _pending?.Cancel();
            _pending = cts;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_delayMs, cts.Token).ConfigureAwait(false);
                _restoreSpace();
            }
            catch (OperationCanceledException) { }
        });
    }

    public void OnNonSpaceKey()
    {
        CancellationTokenSource? toCancel;
        lock (_lock)
        {
            toCancel = _pending;
            _pending = null;
        }
        toCancel?.Cancel();
    }
}
