using AutolumoBarcodeScannerTool.Core.Models;
using AutolumoBarcodeScannerTool.Core.Sinks;
using AutolumoBarcodeScannerTool.Core.Sources;
using AutolumoBarcodeScannerTool.Core.Transforms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutolumoBarcodeScannerTool.Core.Orchestration;

public sealed class ScannerOrchestrator : IAsyncDisposable
{
    private readonly IInputSource _source;
    private readonly ITerminatorTransform _transform;
    private readonly IInputSink _sink;
    private readonly ILogger<ScannerOrchestrator> _logger;
    private readonly IOptionsMonitor<DiagnosticsOptions> _diag;
    private bool _started;

    public ScannerOrchestrator(
        IInputSource source,
        ITerminatorTransform transform,
        IInputSink sink,
        ILogger<ScannerOrchestrator> logger,
        IOptionsMonitor<DiagnosticsOptions> diag)
    {
        _source = source;
        _transform = transform;
        _sink = sink;
        _logger = logger;
        _diag = diag;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_started) return;
        _source.OnScan += HandleScanAsync;
        await _source.StartAsync(ct).ConfigureAwait(false);
        _started = true;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (!_started) return;
        _source.OnScan -= HandleScanAsync;
        await _source.StopAsync(ct).ConfigureAwait(false);
        _started = false;
    }

    private async Task HandleScanAsync(ScanEvent raw)
    {
        try
        {
            var transformed = _transform.Apply(raw);
            if (_diag.CurrentValue.VerboseLogging)
                _logger.LogInformation(
                    "Transform: '{Before}' (len={LenB}) → '{After}' (len={LenA})",
                    Escape(raw.Payload), raw.Payload.Length,
                    Escape(transformed.Payload), transformed.Payload.Length);
            await _sink.SendAsync(transformed, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando escaneo");
        }
    }

    private static string Escape(string s) =>
        s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
    }
}
