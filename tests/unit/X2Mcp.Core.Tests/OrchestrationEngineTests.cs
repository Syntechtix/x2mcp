using NSubstitute;
using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Config;
using X2Mcp.Core.Models;
using X2Mcp.Core.Orchestration;

namespace X2Mcp.Core.Tests;

public class OrchestrationEngineTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"x2mcp-oe-{Guid.NewGuid():N}");

    public OrchestrationEngineTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private OrchestrationEngine MakeEngine(FakeProcessRunner runner, StubLanguageModule module) =>
        new([module], runner, generatedProjectsRoot: Path.Combine(_tempDir, "gen"));

    [Fact]
    public async Task RunAsync_HappyPath_WritesFilesAndReturnsSuccess()
    {
        var sourceDir = CreateSourceDir("src", ".stub");
        var outDir = Path.Combine(_tempDir, "out");
        var fake = new FakeProcessRunner();
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));

        var result = await engine.RunAsync(sourceDir, outDir, "MyServer", Transport.Stdio);

        Assert.True(result.Success);
        Assert.Equal(outDir, result.OutputPath);
        Assert.Null(result.Error);
        // One call to probe toolchain availability, one to actually run the publish command.
        Assert.Equal(2, fake.Calls.Count);
        Assert.Equal("stub-tool", fake.Calls[^1].Executable);
        Assert.True(File.Exists(Path.Combine(_tempDir, "gen", "MyServer", "stub.txt")));
    }

    [Fact]
    public async Task RunAsync_OutputDirectoryDoesNotExist_IsCreatedBeforeToolchainRuns()
    {
        // Regression: some toolchains (e.g. `go build -o`) fail if the output directory
        // doesn't already exist, so the engine must create it up front rather than relying
        // on the underlying build tool to do so.
        var sourceDir = CreateSourceDir("src9", ".stub");
        var outDir = Path.Combine(_tempDir, "does", "not", "exist", "yet");
        var fake = new FakeProcessRunner();
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));

        Assert.False(Directory.Exists(outDir));

        var result = await engine.RunAsync(sourceDir, outDir, "NewDirSvr", Transport.Stdio);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(outDir));
    }

    [Fact]
    public async Task RunAsync_ProcessFails_ReturnsFailure()
    {
        var sourceDir = CreateSourceDir("src2", ".stub");
        var fake = new FakeProcessRunner { DefaultResult = new ProcessResult(1, string.Empty, "build error") };
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));

        var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out2"), "FailSvr", Transport.Stdio);

        Assert.False(result.Success);
        Assert.Equal("build error", result.Error);
    }

    [Fact]
    public async Task RunAsync_PublishCommand_HasTokensResolved()
    {
        var sourceDir = CreateSourceDir("src3", ".stub");
        var outDir = Path.Combine(_tempDir, "out3");
        var fake = new FakeProcessRunner();
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));

        await engine.RunAsync(sourceDir, outDir, "TokSvr", Transport.Stdio);

        var genPath = Path.Combine(_tempDir, "gen", "TokSvr");
        Assert.Contains(genPath, fake.Calls[^1].Arguments);
        Assert.Contains(outDir, fake.Calls[^1].Arguments);
    }

    [Fact]
    public async Task RunAsync_HttpTransport_PassedThroughToContext()
    {
        var sourceDir = CreateSourceDir("src4", ".stub");
        var fake = new FakeProcessRunner();
        var emitted = new EmittedProject(
            Path.Combine(_tempDir, "gen", "HttpSvr"),
            [new EmittedFile("t.txt", "transport={Transport}")]);
        var engine = MakeEngine(fake, new StubLanguageModule(".stub", emittedProject: emitted));

        var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out4"), "HttpSvr", Transport.StreamableHttp);

        // The emitted file content itself isn't token-resolved — context tokens are for commands.
        // Verify engine completed successfully.
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunAsync_ReportsDetectedLanguageAndStdioTransport()
    {
        var sourceDir = CreateSourceDir("src5", ".stub");
        var fake = new FakeProcessRunner();
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));
        var messages = new List<string>();

        await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out5"), "ProgSvr", Transport.Stdio, messages.Add);

        Assert.Equal(
            ["Detected language: stub", "Creating stdio server..."],
            messages);
    }

    [Fact]
    public async Task RunAsync_ReportsHttpTransport()
    {
        var sourceDir = CreateSourceDir("src6", ".stub");
        var fake = new FakeProcessRunner();
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));
        var messages = new List<string>();

        await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out6"), "ProgSvr2", Transport.StreamableHttp, messages.Add);

        Assert.Equal(
            ["Detected language: stub", "Creating http server..."],
            messages);
    }

    [Fact]
    public async Task RunAsync_NoProgressCallback_DoesNotThrow()
    {
        var sourceDir = CreateSourceDir("src7", ".stub");
        var fake = new FakeProcessRunner();
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));

        var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out7"), "ProgSvr3", Transport.Stdio);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunAsync_ProcessFails_ProgressStillReportsBeforeFailure()
    {
        var sourceDir = CreateSourceDir("src8", ".stub");
        var fake = new FakeProcessRunner { DefaultResult = new ProcessResult(1, string.Empty, "build error") };
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));
        var messages = new List<string>();

        var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out8"), "ProgSvr4", Transport.Stdio, messages.Add);

        Assert.False(result.Success);
        Assert.Equal(
            ["Detected language: stub", "Creating stdio server..."],
            messages);
    }

    [Fact]
    public async Task RunAsync_NoGeneratedProjectsRootProvided_DefaultsToSystemTempX2Mcp()
    {
        var sourceDir = CreateSourceDir("src10", ".stub");
        var fake = new FakeProcessRunner();
        var serverName = $"DefaultRootSvr-{Guid.NewGuid():N}";
        var engine = new OrchestrationEngine([new StubLanguageModule(".stub")], fake);
        var expectedGenDir = Path.Combine(Path.GetTempPath(), "x2mcp", serverName);

        try
        {
            var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out10"), serverName, Transport.Stdio);

            Assert.True(result.Success);
            Assert.True(Directory.Exists(expectedGenDir));
        }
        finally
        {
            if (Directory.Exists(expectedGenDir))
                Directory.Delete(expectedGenDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_RequiredToolMissing_ReturnsFailureNamingToolAndFullList()
    {
        var sourceDir = CreateSourceDir("src11", ".stub");
        var fake = new FakeProcessRunner { DefaultResult = new ProcessResult(-1, string.Empty, "not found") };
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));

        var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out11"), "MissingToolSvr", Transport.Stdio);

        Assert.False(result.Success);
        Assert.Equal(
            "Missing required tool for stub: stub-tool. The stub toolchain requires: stub-tool.",
            result.Error);
    }

    [Fact]
    public async Task RunAsync_RequiredToolMissing_DoesNotAttemptScanOrBuild()
    {
        var sourceDir = CreateSourceDir("src12", ".stub");
        var fake = new FakeProcessRunner { DefaultResult = new ProcessResult(-1, string.Empty, "not found") };
        var engine = MakeEngine(fake, new StubLanguageModule(".stub"));

        await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out12"), "MissingToolSvr2", Transport.Stdio);

        Assert.Single(fake.Calls);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "gen", "MissingToolSvr2")));
    }

    [Fact]
    public async Task RunAsync_OneOfMultipleRequiredToolsMissing_ReportsOnlyTheMissingOneAndFullList()
    {
        var sourceDir = CreateSourceDir("src13", ".stub");
        var fake = new FakeProcessRunner
        {
            DefaultResult = new ProcessResult(0, string.Empty, string.Empty),
            ResultsByExecutable = { ["second-tool"] = new ProcessResult(-1, string.Empty, "not found") },
        };
        var module = new StubLanguageModule(".stub", requiredExecutables: ["stub-tool", "second-tool"]);
        var engine = MakeEngine(fake, module);

        var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out13"), "PartialMissingSvr", Transport.Stdio);

        Assert.False(result.Success);
        Assert.Equal(
            "Missing required tool for stub: second-tool. The stub toolchain requires: stub-tool, second-tool.",
            result.Error);
    }

    [Fact]
    public async Task RunAsync_AllRequiredToolsAvailable_ProceedsToBuild()
    {
        var sourceDir = CreateSourceDir("src14", ".stub");
        var fake = new FakeProcessRunner();
        var module = new StubLanguageModule(".stub", requiredExecutables: ["stub-tool", "second-tool"]);
        var engine = MakeEngine(fake, module);

        var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out14"), "AllAvailableSvr", Transport.Stdio);

        Assert.True(result.Success);
        // Two probe calls (stub-tool, second-tool) plus the actual publish invocation.
        Assert.Equal(3, fake.Calls.Count);
    }

    [Fact]
    public async Task RunAsync_MultipleRequiredToolsMissing_UsesPluralWordingAndListsAll()
    {
        var sourceDir = CreateSourceDir("src15", ".stub");
        var fake = new FakeProcessRunner { DefaultResult = new ProcessResult(-1, string.Empty, "not found") };
        var module = new StubLanguageModule(".stub", requiredExecutables: ["stub-tool", "second-tool"]);
        var engine = MakeEngine(fake, module);

        var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out15"), "AllMissingSvr", Transport.Stdio);

        Assert.False(result.Success);
        Assert.Equal(
            "Missing required tools for stub: stub-tool, second-tool. The stub toolchain requires: stub-tool, second-tool.",
            result.Error);
    }

    [Fact]
    public async Task RunAsync_ExplicitToolchainValidatorProvided_IsUsedInsteadOfDefault()
    {
        var sourceDir = CreateSourceDir("src16", ".stub");
        var fake = new FakeProcessRunner();
        var validator = Substitute.For<IToolchainValidator>();
        validator.FindMissingExecutablesAsync(Arg.Any<ToolchainConfig>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>([]));
        var engine = new OrchestrationEngine(
            [new StubLanguageModule(".stub")],
            fake,
            generatedProjectsRoot: Path.Combine(_tempDir, "gen"),
            toolchainValidator: validator);

        var result = await engine.RunAsync(sourceDir, Path.Combine(_tempDir, "out16"), "CustomValidatorSvr", Transport.Stdio);

        Assert.True(result.Success);
        await validator.Received(1).FindMissingExecutablesAsync(Arg.Any<ToolchainConfig>(), Arg.Any<CancellationToken>());
    }

    private string CreateSourceDir(string name, string ext)
    {
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"file{ext}"), "content");
        return dir;
    }
}
