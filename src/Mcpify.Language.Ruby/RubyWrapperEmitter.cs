using Mcpify.Core.Abstractions;
using Mcpify.Core.Models;

namespace Mcpify.Language.Ruby;

public class RubyWrapperEmitter : IWrapperEmitter
{
    public EmittedProject Emit(ScannedSurface surface, BuildContext context) =>
        throw new NotImplementedException("Ruby wrapper emitter is not yet implemented.");
}
