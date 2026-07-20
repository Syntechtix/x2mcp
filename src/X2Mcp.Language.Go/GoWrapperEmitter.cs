using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Go;

public class GoWrapperEmitter : IWrapperEmitter
{
    public EmittedProject Emit(ScannedSurface surface, BuildContext context) =>
        throw new NotImplementedException("Go wrapper emitter is not yet implemented.");
}
