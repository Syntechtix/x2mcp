using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Go;

public class GoScanner : IScanner
{
    public ScannedSurface Scan(string sourcePath) =>
        throw new NotImplementedException("Go scanner is not yet implemented.");
}
