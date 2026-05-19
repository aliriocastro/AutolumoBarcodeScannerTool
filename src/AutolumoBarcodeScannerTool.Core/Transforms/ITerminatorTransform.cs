using AutolumoBarcodeScannerTool.Core.Models;

namespace AutolumoBarcodeScannerTool.Core.Transforms;

public interface ITerminatorTransform
{
    ScanEvent Apply(ScanEvent input);
}
