using AutolumoBarcodeScannerTool.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutolumoBarcodeScannerTool.Core.Sinks;

public sealed class ForegroundProcessSink : IInputSink
{
    private readonly IOptionsMonitor<TargetOptions> _opts;
    private readonly IOptionsMonitor<DiagnosticsOptions> _diag;
    private readonly IForegroundWindowProvider _window;
    private readonly IInputInjector _injector;
    private readonly ILogger<ForegroundProcessSink> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Single ctor: IOptionsMonitor only. A second IOptions ctor would create a
    // DI ambiguity at resolution time — IServiceProvider.GetService<T>() does
    // NOT honor [ActivatorUtilitiesConstructor] (that attribute only applies to
    // ActivatorUtilities.CreateInstance), so we cannot use it to disambiguate.
    public ForegroundProcessSink(
        IOptionsMonitor<TargetOptions> opts,
        IOptionsMonitor<DiagnosticsOptions> diag,
        IForegroundWindowProvider window,
        IInputInjector injector,
        ILogger<ForegroundProcessSink> logger)
    {
        _opts = opts;
        _diag = diag;
        _window = window;
        _injector = injector;
        _logger = logger;
    }

    public async Task SendAsync(ScanEvent ev, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false))
        {
            _logger.LogWarning("Escaneo descartado: sink ocupado con escaneo previo (payload len={Len})", ev.Payload.Length);
            return;
        }

        try
        {
            var target = _opts.CurrentValue;
            var current = _window.GetCurrent();

            if (current is null)
            {
                _logger.LogInformation("Escaneo descartado: no hay ventana en foreground");
                return;
            }

            if (!ProcessMatches(current.ProcessName, target.ProcessName))
            {
                _logger.LogInformation(
                    "Escaneo descartado: ventana activa '{Current}' no coincide con target '{Target}'",
                    current.ProcessName, target.ProcessName);
                return;
            }

            if (!string.IsNullOrEmpty(target.WindowTitleContains) &&
                current.WindowTitle.IndexOf(target.WindowTitleContains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                _logger.LogInformation(
                    "Escaneo descartado: título '{Title}' no contiene '{Needle}'",
                    current.WindowTitle, target.WindowTitleContains);
                return;
            }

            if (_diag.CurrentValue.VerboseLogging)
                _logger.LogInformation(
                    "Inyectando hacia '{Proc}'/'{Title}' payload='{Payload}' (len={Len})",
                    current.ProcessName, current.WindowTitle,
                    ev.Payload.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"),
                    ev.Payload.Length);

            // SendInput is a fast Win32 syscall; running inline avoids both
            // threadpool overhead and TOCTOU where the foreground window could
            // change between the GetCurrent() check and the actual injection.
            _injector.SendText(ev.Payload);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool ProcessMatches(string current, string target)
    {
        if (string.IsNullOrEmpty(target)) return false;
        return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
    }
}
