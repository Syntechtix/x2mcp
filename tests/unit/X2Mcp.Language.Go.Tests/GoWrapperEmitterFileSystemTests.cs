using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;
using X2Mcp.Language.Go;

namespace X2Mcp.Language.Go.Tests;

public class GoWrapperEmitterFileSystemTests
{
    private static BuildContext MakeContext(
        string sourcePath,
        Transport transport = Transport.Stdio,
        string serverName = "TestServer") =>
        new(sourcePath, "/out", $"/gen/{serverName}", serverName, transport);

    private static ScannedSurface EmptySurface(string sourcePath = "/fake/src") =>
        new(sourcePath, "go", []);

    [Fact]
    public void Emit_SourceIsDirectory_FindsGoModInSameDirectory()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/proj/src").Returns(false);
        fs.DirectoryExists("/proj/src").Returns(true);
        fs.FileExists("/proj/src/go.mod").Returns(true);
        fs.ReadAllText("/proj/src/go.mod").Returns("module example.com/lib\n\ngo 1.23\n");

        var context = MakeContext("/proj/src");
        var project = new GoWrapperEmitter(fs).Emit(EmptySurface("/proj/src"), context);

        var goMod = project.Files.Single(f => f.RelativePath == "go.mod").Content;
        Assert.Contains("example.com/lib v0.0.0", goMod);
    }

    [Fact]
    public void Emit_SourceIsFile_ResolvesParentDirectoryForGoMod()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/proj/src/lib.go").Returns(true);
        fs.FileExists("/proj/src/go.mod").Returns(true);
        fs.ReadAllText("/proj/src/go.mod").Returns("module example.com/lib\n\ngo 1.23\n");

        var context = MakeContext("/proj/src/lib.go");
        var project = new GoWrapperEmitter(fs).Emit(EmptySurface("/proj/src/lib.go"), context);

        var mainGo = project.Files.Single(f => f.RelativePath == "main.go").Content;
        Assert.Contains("srcpkg \"example.com/lib\"", mainGo);
    }

    [Fact]
    public void Emit_GoModInParentDirectory_WalksUpDirectoryTree()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/proj/src/pkg").Returns(false);
        fs.FileExists("/proj/src/pkg/go.mod").Returns(false);
        fs.FileExists("/proj/src/go.mod").Returns(true);
        fs.ReadAllText("/proj/src/go.mod").Returns("module example.com/lib\n\ngo 1.23\n");

        var context = MakeContext("/proj/src/pkg");
        var project = new GoWrapperEmitter(fs).Emit(EmptySurface("/proj/src/pkg"), context);

        var mainGo = project.Files.Single(f => f.RelativePath == "main.go").Content;
        Assert.Contains("srcpkg \"example.com/lib/pkg\"", mainGo);
    }

    [Fact]
    public void Emit_NoGoModAnywhereUpTheTree_ThrowsInvalidOperationException()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(Arg.Any<string>()).Returns(false);

        var context = MakeContext("/proj/src/pkg");

        Assert.Throws<InvalidOperationException>(
            () => new GoWrapperEmitter(fs).Emit(EmptySurface("/proj/src/pkg"), context));
    }

    [Fact]
    public void Emit_HttpTransport_FileSystemNotQueriedForTransportLogic()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.FileExists("/src/go.mod").Returns(true);
        fs.ReadAllText("/src/go.mod").Returns("module example.com/lib\n\ngo 1.23\n");

        var context = MakeContext("/src", Transport.StreamableHttp);
        var project = new GoWrapperEmitter(fs).Emit(EmptySurface("/src"), context);

        var mainGo = project.Files.Single(f => f.RelativePath == "main.go").Content;
        Assert.Contains("NewStreamableHTTPHandler", mainGo);
    }
}
