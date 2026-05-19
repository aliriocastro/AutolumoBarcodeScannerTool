using System.Text;
using AutolumoBarcodeScannerTool.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutolumoBarcodeScannerTool.Core.Sources.Serial;

public sealed class SerialInputSource : IInputSource
{
    private readonly ISerialPortAdapter _port;
    private readonly ScanTerminator _terminator;
    private readonly Encoding _encoding;
    private readonly ILogger<SerialInputSource> _logger;
    private readonly StringBuilder _buffer = new();
    private readonly object _bufferLock = new();
    private SourceConnectionState _state = SourceConnectionState.Disconnected;

    public event Func<ScanEvent, Task>? OnScan;
    public event EventHandler<SourceConnectionState>? StateChanged;

    public SerialInputSource(
        ISerialPortAdapter port,
        ScanTerminator terminator,
        Encoding encoding,
        ILogger<SerialInputSource> logger)
    {
        _port = port;
        _terminator = terminator;
        _encoding = encoding;
        _logger = logger;
        _port.DataReceived += OnDataReceived;
    }

    public SourceConnectionState State => _state;

    public Task StartAsync(CancellationToken ct)
    {
        try
        {
            _port.Open();
            SetState(SourceConnectionState.Connected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error abriendo puerto serial");
            SetState(SourceConnectionState.Error);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _port.Close();
        SetState(SourceConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    private void OnDataReceived(object? sender, ReadOnlyMemory<byte> bytes)
    {
        var text = _encoding.GetString(bytes.Span);

        List<ScanEvent>? events = null;
        lock (_bufferLock)
        {
            _buffer.Append(text);
            events = TryExtractEvents();
        }

        if (events is null) return;
        foreach (var ev in events)
            OnScan?.Invoke(ev);
    }

    private List<ScanEvent>? TryExtractEvents()
    {
        List<ScanEvent>? results = null;
        while (true)
        {
            var current = _buffer.ToString();
            var (terminatorIndex, terminatorLength, detected) = FindTerminator(current);
            if (terminatorIndex < 0) break;

            var payload = current[..terminatorIndex];
            _buffer.Clear();
            _buffer.Append(current.AsSpan(terminatorIndex + terminatorLength));

            results ??= new List<ScanEvent>();
            results.Add(new ScanEvent(payload, detected, DateTimeOffset.UtcNow));
        }
        return results;
    }

    private (int Index, int Length, ScanTerminator Detected) FindTerminator(string s)
    {
        switch (_terminator)
        {
            case ScanTerminator.CrLf:
                {
                    var i = s.IndexOf("\r\n", StringComparison.Ordinal);
                    return i >= 0 ? (i, 2, ScanTerminator.CrLf) : (-1, 0, default);
                }
            case ScanTerminator.Cr:
                {
                    var i = s.IndexOf('\r');
                    return i >= 0 ? (i, 1, ScanTerminator.Cr) : (-1, 0, default);
                }
            case ScanTerminator.Lf:
                {
                    var i = s.IndexOf('\n');
                    return i >= 0 ? (i, 1, ScanTerminator.Lf) : (-1, 0, default);
                }
            case ScanTerminator.Any:
                {
                    var crLf = s.IndexOf("\r\n", StringComparison.Ordinal);
                    if (crLf >= 0) return (crLf, 2, ScanTerminator.CrLf);
                    var cr = s.IndexOf('\r');
                    var lf = s.IndexOf('\n');
                    if (cr >= 0 && (lf < 0 || cr < lf)) return (cr, 1, ScanTerminator.Cr);
                    if (lf >= 0) return (lf, 1, ScanTerminator.Lf);
                    return (-1, 0, default);
                }
            default:
                return (-1, 0, default);
        }
    }

    private void SetState(SourceConnectionState s)
    {
        if (_state == s) return;
        _state = s;
        StateChanged?.Invoke(this, s);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _port.DataReceived -= OnDataReceived;
        _port.Dispose();
    }
}
