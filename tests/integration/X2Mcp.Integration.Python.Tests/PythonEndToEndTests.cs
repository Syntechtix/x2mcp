using X2Mcp.Core.Models;
using X2Mcp.Core.Orchestration;
using X2Mcp.Core.Process;
using X2Mcp.Language.Python;

namespace X2Mcp.Integration.Python.Tests;

[Trait("Category", "Integration")]
public class PythonEndToEndTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"x2mcp-python-e2e-{Guid.NewGuid():N}");

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public PythonEndToEndTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task WrapPythonSampleLib_Stdio_ProducesExecutable()
    {
        var outputDir = Path.Combine(_tempDir, "output");
        var genRoot = Path.Combine(_tempDir, "gen");

        var engine = new OrchestrationEngine(
            [new PythonModule()],
            new ProcessRunner(),
            generatedProjectsRoot: genRoot);

        var result = await engine.RunAsync(
            FixturesDir,
            outputDir,
            "PythonSampleLib",
            Transport.Stdio);

        Assert.True(result.Success, $"Build failed: {result.Error}");
        Assert.True(Directory.Exists(outputDir), "Output directory was not created.");

        var binaryName = OperatingSystem.IsWindows() ? "PythonSampleLib.exe" : "PythonSampleLib";
        Assert.True(
            File.Exists(Path.Combine(outputDir, binaryName)),
            $"Binary not found at {Path.Combine(outputDir, binaryName)}");
    }
}
