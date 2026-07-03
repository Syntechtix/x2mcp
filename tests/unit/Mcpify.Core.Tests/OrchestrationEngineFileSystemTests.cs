using Mcpify.Core.Abstractions;
using Mcpify.Core.Models;
using Mcpify.Core.Orchestration;

namespace Mcpify.Core.Tests;

public class OrchestrationEngineFileSystemTests
{
    private static OrchestrationEngine MakeEngine(
        IFileSystem fileSystem,
        FakeProcessRunner processRunner,
        StubLanguageModule module,
        string genRoot = "/gen") =>
        new([module], processRunner, fileSystem, generatedProjectsRoot: genRoot);

    [Fact]
    public async Task RunAsync_FileSource_DetectsExtensionWithoutDirectoryListing()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/file.stub").Returns(true);
        fs.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var runner = new FakeProcessRunner();
        var engine = MakeEngine(fs, runner, new StubLanguageModule(".stub"));

        var result = await engine.RunAsync("/src/file.stub", "/out", "Srv", Transport.Stdio);

        Assert.True(result.Success);
        fs.DidNotReceive().GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [Fact]
    public async Task RunAsync_DirectorySource_ListsFilesToDetectExtensions()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*", SearchOption.AllDirectories)
            .Returns(["/src/code.stub"]);
        fs.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var runner = new FakeProcessRunner();
        var engine = MakeEngine(fs, runner, new StubLanguageModule(".stub"));

        var result = await engine.RunAsync("/src", "/out", "Srv", Transport.Stdio);

        Assert.True(result.Success);
        fs.Received(1).GetFiles("/src", "*", SearchOption.AllDirectories);
    }

    [Fact]
    public async Task RunAsync_EmitsFiles_CallsCreateDirectoryAndWrite()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/file.stub").Returns(true);
        fs.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var runner = new FakeProcessRunner();
        var engine = MakeEngine(fs, runner, new StubLanguageModule(".stub"), genRoot: "/gen");

        await engine.RunAsync("/src/file.stub", "/out", "MySvr", Transport.Stdio);

        fs.Received().CreateDirectory(Arg.Any<string>());
        await fs.Received().WriteAllTextAsync(
            Arg.Is<string>(p => p.Contains("MySvr")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_EmptySource_ThrowsNoLanguageModuleException()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*", SearchOption.AllDirectories).Returns([]);

        var runner = new FakeProcessRunner();
        var engine = MakeEngine(fs, runner, new StubLanguageModule(".stub"));

        await Assert.ThrowsAsync<NoLanguageModuleException>(
            () => engine.RunAsync("/src", "/out", "Srv", Transport.Stdio));
    }

    [Fact]
    public async Task RunAsync_ProcessFails_ReturnsErrorFromStderr()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/file.stub").Returns(true);
        fs.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var runner = new FakeProcessRunner
        {
            DefaultResult = new ProcessResult(1, string.Empty, "build failed"),
        };
        var engine = MakeEngine(fs, runner, new StubLanguageModule(".stub"));

        var result = await engine.RunAsync("/src/file.stub", "/out", "Srv", Transport.Stdio);

        Assert.False(result.Success);
        Assert.Equal("build failed", result.Error);
    }
}
