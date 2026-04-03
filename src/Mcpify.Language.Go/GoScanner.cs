using Mcpify.Core.Abstractions;
using Mcpify.Core.Models;

namespace Mcpify.Language.Go;

public class GoScanner : IScanner
{
    public ScannedSurface Scan(string sourcePath) =>
        throw new NotImplementedException("Go scanner is not yet implemented.");
}
