using Mcpify.Core.Models;

namespace Mcpify.Core.Abstractions;

public interface IScanner
{
    ScannedSurface Scan(string sourcePath);
}
