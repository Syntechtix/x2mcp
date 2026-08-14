using X2Mcp.Core.Models;
using X2Mcp.Core.Orchestration;
using X2Mcp.Core.Process;
using X2Mcp.Language.Rust;

namespace X2Mcp.Integration.Rust.Tests;

/// <summary>
/// End-to-end tests that run the full scan → emit → build pipeline.
/// These require cargo on PATH and network access for crates.io resolution.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class RustEndToEndTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"x2mcp-rust-e2e-{Guid.NewGuid():N}");

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "RustSampleLib");

    public RustEndToEndTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task WrapRustSampleLib_Stdio_ProducesExecutable()
    {
        var outputDir = Path.Combine(_tempDir, "output");
        var genRoot = Path.Combine(_tempDir, "gen");

        var engine = new OrchestrationEngine(
            [new RustModule()],
            new ProcessRunner(),
            generatedProjectsRoot: genRoot);

        var result = await engine.RunAsync(
            FixturesDir,
            outputDir,
            "RustSampleLib",
            Transport.Stdio);

        Assert.True(result.Success, $"Build failed: {result.Error}");

        // cargo install --root places the binary under <root>/bin/, unlike the flat layout other languages use.
        var binaryName = OperatingSystem.IsWindows() ? "RustSampleLib.exe" : "RustSampleLib";
        var binaryPath = Path.Combine(outputDir, "bin", binaryName);
        Assert.True(File.Exists(binaryPath), $"Binary not found at {binaryPath}");
    }
}
