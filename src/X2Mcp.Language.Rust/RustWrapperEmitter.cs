using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Rust;

public class RustWrapperEmitter : IWrapperEmitter
{
    public EmittedProject Emit(ScannedSurface surface, BuildContext context) =>
        throw new NotImplementedException("Rust wrapper emitter is not yet implemented.");
}
