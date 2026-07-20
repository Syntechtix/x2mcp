using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Ruby;

public class RubyScanner : IScanner
{
    public ScannedSurface Scan(string sourcePath) =>
        throw new NotImplementedException("Ruby scanner is not yet implemented.");
}
