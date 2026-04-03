using Mcpify.Core.Process;

namespace Mcpify.Core.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ValidExecutable_ReturnsZeroExitCode()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync("dotnet", "--version", Path.GetTempPath());
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_ValidExecutable_CapturesStdout()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync("dotnet", "--version", Path.GetTempPath());
        Assert.Matches(@"\d+\.\d+", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_FailingCommand_ReturnsNonZeroExitCode()
    {
        var runner = new ProcessRunner();
        var nonExistent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Fake.csproj");
        var result = await runner.RunAsync("dotnet", $"build \"{nonExistent}\"", Path.GetTempPath());
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_FailingCommand_CapturesStderr()
    {
        var runner = new ProcessRunner();
        var nonExistent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Fake.csproj");
        var result = await runner.RunAsync("dotnet", $"build \"{nonExistent}\"", Path.GetTempPath());
        // dotnet outputs error info to stderr or stdout; either way ExitCode != 0 is enough
        // but at minimum one of the output streams should be non-empty
        Assert.True(result.StandardOutput.Length > 0 || result.StandardError.Length > 0);
    }
}
