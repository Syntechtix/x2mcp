using Mcpify.Core.Abstractions;
using Mcpify.Core.Models;

namespace Mcpify.Language.Rust;

public class RustScanner : IScanner
{
    public ScannedSurface Scan(string sourcePath) =>
        throw new NotImplementedException("Rust scanner is not yet implemented.");
}
