using X2Mcp.Core.Models;

namespace X2Mcp.Core.Abstractions;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        string workingDirectory,
        CancellationToken ct = default);
}
