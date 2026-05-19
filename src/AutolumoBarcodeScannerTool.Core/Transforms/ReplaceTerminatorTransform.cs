using AutolumoBarcodeScannerTool.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AutolumoBarcodeScannerTool.Core.Transforms;

public sealed class ReplaceTerminatorTransform : ITerminatorTransform
{
    private readonly IOptionsMonitor<OutputOptions> _opts;

    // Test-friendly constructor (IOptions). Marcado as NO el preferido por DI.
    public ReplaceTerminatorTransform(IOptions<OutputOptions> opts)
        : this(new StaticOptionsMonitor<OutputOptions>(opts.Value)) { }

    // Constructor preferido por DI en producción (hot-reload).
    [ActivatorUtilitiesConstructor]
    public ReplaceTerminatorTransform(IOptionsMonitor<OutputOptions> opts)
    {
        _opts = opts;
    }

    public ScanEvent Apply(ScanEvent input) =>
        input with { Payload = input.Payload + ResolveSuffix(_opts.CurrentValue) };

    private static string ResolveSuffix(OutputOptions opts) => opts.OnTerminator switch
    {
        OutputMode.Tab => "\t",
        OutputMode.TabEnter => "\t\n",
        OutputMode.TabOnly => string.Empty,
        OutputMode.Custom => (opts.OutputSuffix ?? string.Empty)
            .Replace("{TAB}", "\t", StringComparison.Ordinal)
            .Replace("{ENTER}", "\n", StringComparison.Ordinal),
        _ => "\t"
    };

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) { CurrentValue = value; }
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
