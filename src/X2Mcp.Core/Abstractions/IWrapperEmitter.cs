using X2Mcp.Core.Models;

namespace X2Mcp.Core.Abstractions;

public interface IWrapperEmitter
{
    EmittedProject Emit(ScannedSurface surface, BuildContext context);
}
