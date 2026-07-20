using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Python;

public class PythonWrapperEmitter : IWrapperEmitter
{
    public EmittedProject Emit(ScannedSurface surface, BuildContext context) =>
        throw new NotImplementedException("Python wrapper emitter is not yet implemented.");
}
