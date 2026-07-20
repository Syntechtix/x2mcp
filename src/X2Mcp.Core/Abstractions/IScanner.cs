using X2Mcp.Core.Models;

namespace X2Mcp.Core.Abstractions;

public interface IScanner
{
    ScannedSurface Scan(string sourcePath);
}
