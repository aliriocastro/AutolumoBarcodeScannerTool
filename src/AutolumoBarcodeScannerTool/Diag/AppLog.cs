using System.IO;

namespace AutolumoBarcodeScannerTool.Diag;

// Minimal append-only file logger. Sin Serilog, sin batching, sin async.
// Suficiente para diagnosticar problemas en el equipo de producción.
//
// `Info` siempre escribe (eventos de arranque, conexión del lector, etc.).
// `Debug` sólo cuando Verbose=true (cada WM_INPUT, cada decisión del LL hook).
// Las excepciones de escritura se tragan — el logging NUNCA debe crashear
// la app (estamos en el LL hook callback en parte del tiempo).
internal static class AppLog
{
    private static readonly object Sync = new();
    private static string? _logDir;
    private static bool _verbose;

    public static void Init(string logDir, bool verbose)
    {
        _logDir = logDir;
        _verbose = verbose;
        try { Directory.CreateDirectory(logDir); }
        catch { /* swallow */ }
    }

    public static void SetVerbose(bool verbose) => _verbose = verbose;

    public static void Info(string message) => Write("INF", message);

    public static void Debug(string message)
    {
        if (!_verbose) return;
        Write("DBG", message);
    }

    private static void Write(string level, string message)
    {
        if (_logDir is null) return;
        var line = $"[{DateTime.Now:HH:mm:ss.fff} {level}] {message}\n";
        var path = Path.Combine(_logDir, $"app-{DateTime.Now:yyyyMMdd}.log");
        try
        {
            lock (Sync) File.AppendAllText(path, line);
        }
        catch { /* swallow — logging must never crash the app */ }
    }
}
