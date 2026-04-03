using Mcpify.Core.Abstractions;
using Mcpify.Core.Models;

namespace Mcpify.Language.Ruby;

public class RubyScanner : IScanner
{
    public ScannedSurface Scan(string sourcePath) =>
        throw new NotImplementedException("Ruby scanner is not yet implemented.");
}
