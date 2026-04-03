using Mcpify.Core.Models;
using Mcpify.Core.Orchestration;

namespace Mcpify.Core.Tests;

public class LanguageDetectionTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"mcpify-det-{Guid.NewGuid():N}");

    public LanguageDetectionTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_UnknownExtension_ThrowsNoLanguageModuleException()
    {
        File.WriteAllText(Path.Combine(_tempDir, "foo.xyz"), "content");
        var engine = new OrchestrationEngine(
            [new StubLanguageModule(".cs")],
            new FakeProcessRunner(),
            generatedProjectsRoot: Path.Combine(_tempDir, "gen"));

        var ex = await Assert.ThrowsAsync<NoLanguageModuleException>(
            () => engine.RunAsync(_tempDir, Path.Combine(_tempDir, "out"), "T", Transport.Stdio));

        Assert.Contains(".xyz", ex.DetectedExtensions, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(_tempDir, ex.SourcePath);
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public async Task RunAsync_EmptyDirectory_ThrowsWithEmptyExtensions()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);
        var engine = new OrchestrationEngine(
            [new StubLanguageModule(".cs")],
            new FakeProcessRunner(),
            generatedProjectsRoot: Path.Combine(_tempDir, "gen"));

        var ex = await Assert.ThrowsAsync<NoLanguageModuleException>(
            () => engine.RunAsync(emptyDir, Path.Combine(_tempDir, "out"), "T", Transport.Stdio));

        Assert.Empty(ex.DetectedExtensions);
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public async Task RunAsync_MatchingExtension_SelectsCorrectModule()
    {
        File.WriteAllText(Path.Combine(_tempDir, "foo.py"), "content");
        var fake = new FakeProcessRunner();
        var engine = new OrchestrationEngine(
            [new StubLanguageModule(".cs"), new StubLanguageModule(".py")],
            fake,
            generatedProjectsRoot: Path.Combine(_tempDir, "gen"));

        var result = await engine.RunAsync(
            _tempDir, Path.Combine(_tempDir, "out"), "T", Transport.Stdio);

        Assert.True(result.Success);
        Assert.Equal("stub-tool", fake.Calls[0].Executable);
    }

    [Fact]
    public async Task RunAsync_SingleFileSource_DetectsExtension()
    {
        var filePath = Path.Combine(_tempDir, "script.stub");
        File.WriteAllText(filePath, "content");
        var fake = new FakeProcessRunner();
        var engine = new OrchestrationEngine(
            [new StubLanguageModule(".stub")],
            fake,
            generatedProjectsRoot: Path.Combine(_tempDir, "gen"));

        var result = await engine.RunAsync(
            filePath, Path.Combine(_tempDir, "out"), "T", Transport.Stdio);

        Assert.True(result.Success);
    }
}
