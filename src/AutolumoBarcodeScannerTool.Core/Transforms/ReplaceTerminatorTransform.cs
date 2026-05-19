using AutolumoBarcodeScannerTool.Core.Models;

namespace AutolumoBarcodeScannerTool.Core.Transforms;

// Hardcoded behavior: drop the first ASCII space from the payload (the barcode's
// internal separator must NOT be typed into the target field) and append ENTER
// at the end so the form advances. The terminator detected by the source is not
// used here — the source already consumed it before raising the event.
public sealed class ReplaceTerminatorTransform : ITerminatorTransform
{
    public ScanEvent Apply(ScanEvent input)
    {
        var payload = input.Payload;
        var idx = payload.IndexOf(' ');
        if (idx >= 0) payload = payload.Remove(idx, 1);
        return input with { Payload = payload + "\n" };
    }
}
