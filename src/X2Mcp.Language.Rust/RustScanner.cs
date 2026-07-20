using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Rust;

public class RustScanner : IScanner
{
    public ScannedSurface Scan(string sourcePath) =>
        throw new NotImplementedException("Rust scanner is not yet implemented.");
}
