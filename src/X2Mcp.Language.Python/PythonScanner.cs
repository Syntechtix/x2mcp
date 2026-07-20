using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Python;

public class PythonScanner : IScanner
{
    public ScannedSurface Scan(string sourcePath) =>
        throw new NotImplementedException("Python scanner is not yet implemented.");
}
