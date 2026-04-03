using Mcpify.Core.Abstractions;
using Mcpify.Core.Models;

namespace Mcpify.Language.Python;

public class PythonScanner : IScanner
{
    public ScannedSurface Scan(string sourcePath) =>
        throw new NotImplementedException("Python scanner is not yet implemented.");
}
