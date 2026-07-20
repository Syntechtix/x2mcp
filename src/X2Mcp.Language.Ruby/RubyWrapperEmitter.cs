using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Ruby;

public class RubyWrapperEmitter : IWrapperEmitter
{
    public EmittedProject Emit(ScannedSurface surface, BuildContext context) =>
        throw new NotImplementedException("Ruby wrapper emitter is not yet implemented.");
}
