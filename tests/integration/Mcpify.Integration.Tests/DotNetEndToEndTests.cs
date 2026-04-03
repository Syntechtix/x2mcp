using Mcpify.Core.Models;
using Mcpify.Core.Orchestration;
using Mcpify.Core.Process;
using Mcpify.Language.DotNet;

namespace Mcpify.Integration.Tests;

/// <summary>
/// End-to-end tests that run the full scan → emit → build pipeline.
/// These require dotnet on PATH and network access for NuGet restore.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class DotNetEndToEndTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"mcpify-e2e-{Guid.NewGuid():N}");

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleLib");

    public DotNetEndToEndTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact(Skip = "Requires dotnet on PATH and NuGet network access. Remove Skip to run manually.")]
    public async Task WrapSampleLib_Stdio_ProducesExecutable()
    {
        var outputDir = Path.Combine(_tempDir, "output");
        var genRoot = Path.Combine(_tempDir, "gen");

        var engine = new OrchestrationEngine(
            [new DotNetModule()],
            new ProcessRunner(),
            generatedProjectsRoot: genRoot);

        var result = await engine.RunAsync(
            FixturesDir,
            outputDir,
            "SampleLib",
            Transport.Stdio);

        Assert.True(result.Success, $"Build failed: {result.Error}");
        Assert.True(Directory.Exists(outputDir), "Output directory was not created.");

        var binaryName = OperatingSystem.IsWindows() ? "SampleLib.exe" : "SampleLib";
        Assert.True(
            File.Exists(Path.Combine(outputDir, binaryName)),
            $"Binary not found at {Path.Combine(outputDir, binaryName)}");
    }
}
