using Mcpify.Core.Abstractions;
using Mcpify.Core.Models;

namespace Mcpify.Core.Tests;

public sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string Executable, string Arguments, string WorkingDirectory)> Calls { get; } = [];
    public ProcessResult DefaultResult { get; init; } = new(0, string.Empty, string.Empty);

    public Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        string workingDirectory,
        CancellationToken ct = default)
    {
        Calls.Add((executable, arguments, workingDirectory));
        return Task.FromResult(DefaultResult);
    }
}
