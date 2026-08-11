using X2Mcp.Core.Models;
using X2Mcp.Core.Orchestration;
using X2Mcp.Core.Process;
using X2Mcp.Language.Go;

namespace X2Mcp.Integration.Go.Tests;

/// <summary>
/// End-to-end tests that run the full scan → emit → build pipeline.
/// These require go on PATH and network access for module resolution.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class GoEndToEndTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"x2mcp-go-e2e-{Guid.NewGuid():N}");

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "GoSampleLib");

    public GoEndToEndTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task WrapGoSampleLib_Stdio_ProducesExecutable()
    {
        var outputDir = Path.Combine(_tempDir, "output");
        var genRoot = Path.Combine(_tempDir, "gen");

        var engine = new OrchestrationEngine(
            [new GoModule()],
            new ProcessRunner(),
            generatedProjectsRoot: genRoot);

        var result = await engine.RunAsync(
            FixturesDir,
            outputDir,
            "GoSampleLib",
            Transport.Stdio);

        Assert.True(result.Success, $"Build failed: {result.Error}");
        Assert.True(Directory.Exists(outputDir), "Output directory was not created.");

        var binaryName = OperatingSystem.IsWindows() ? "GoSampleLib.exe" : "GoSampleLib";
        Assert.True(
            File.Exists(Path.Combine(outputDir, binaryName)),
            $"Binary not found at {Path.Combine(outputDir, binaryName)}");
    }
}
