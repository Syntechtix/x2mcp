using Mcpify.Core.Models;

namespace Mcpify.Core.Abstractions;

public interface IWrapperEmitter
{
    EmittedProject Emit(ScannedSurface surface, BuildContext context);
}
