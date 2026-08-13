using X2Mcp.Core.Models;
using X2Mcp.Core.Orchestration;
using X2Mcp.Core.Process;
using X2Mcp.Language.Ruby;

namespace X2Mcp.Integration.Ruby.Tests;

[Trait("Category", "Integration")]
public class RubyEndToEndTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"x2mcp-ruby-e2e-{Guid.NewGuid():N}");

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public RubyEndToEndTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task WrapRubySampleLib_Stdio_ProducesLauncher()
    {
        var outputDir = Path.Combine(_tempDir, "output");
        var genRoot = Path.Combine(_tempDir, "gen");

        var engine = new OrchestrationEngine(
            [new RubyModule()],
            new ProcessRunner(),
            generatedProjectsRoot: genRoot);

        var result = await engine.RunAsync(
            FixturesDir,
            outputDir,
            "RubySampleLib",
            Transport.Stdio);

        Assert.True(result.Success, $"Build failed: {result.Error}");
        Assert.True(Directory.Exists(outputDir), "Output directory was not created.");

        var launcherName = OperatingSystem.IsWindows() ? "RubySampleLib.cmd" : "RubySampleLib";
        Assert.True(
            File.Exists(Path.Combine(outputDir, launcherName)),
            $"Launcher not found at {Path.Combine(outputDir, launcherName)}");

        Assert.True(
            File.Exists(Path.Combine(outputDir, "RubySampleLib_bundle", "server.rb")),
            "Packaged server.rb was not found.");
    }
}
