using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;
using X2Mcp.Language.DotNet;

namespace X2Mcp.Language.DotNet.Tests;

public class DotNetWrapperEmitterFileSystemTests
{
    private static BuildContext MakeContext(
        string sourcePath,
        Transport transport = Transport.Stdio,
        string serverName = "TestServer") =>
        new(sourcePath, "/out", $"/gen/{serverName}", serverName, transport);

    private static ScannedSurface EmptySurface(string sourcePath = "/fake/src") =>
        new(sourcePath, "csharp", []);

    [Fact]
    public void Emit_SourceIsFile_ResolvesParentDirectoryForCsproj()
    {
        var fs = Substitute.For<IFileSystem>();
        var dir = Path.GetDirectoryName("/proj/src/Class.cs")!;
        fs.FileExists("/proj/src/Class.cs").Returns(true);
        fs.DirectoryExists(dir).Returns(true);
        fs.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly)
            .Returns([Path.Combine(dir, "MyLib.csproj")]);

        var context = MakeContext("/proj/src/Class.cs");
        var project = new DotNetWrapperEmitter(fs).Emit(EmptySurface("/proj/src/Class.cs"), context);

        var csproj = project.Files.Single(f => f.RelativePath == "TestServer.csproj").Content;
        Assert.Contains("MyLib.csproj", csproj);
    }

    [Fact]
    public void Emit_SourceIsDirectory_SearchesCsprojInThatDirectory()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/proj/src").Returns(false);
        fs.DirectoryExists("/proj/src").Returns(true);
        fs.GetFiles("/proj/src", "*.csproj", SearchOption.TopDirectoryOnly)
            .Returns(["/proj/src/Lib.csproj"]);

        var context = MakeContext("/proj/src");
        var project = new DotNetWrapperEmitter(fs).Emit(EmptySurface("/proj/src"), context);

        fs.Received(1).GetFiles("/proj/src", "*.csproj", SearchOption.TopDirectoryOnly);
        var csproj = project.Files.Single(f => f.RelativePath == "TestServer.csproj").Content;
        Assert.Contains("Lib.csproj", csproj);
    }

    [Fact]
    public void Emit_NoCsprojFound_UsesConventionalDirectoryName()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/proj/MyLib").Returns(false);
        fs.DirectoryExists("/proj/MyLib").Returns(true);
        fs.GetFiles("/proj/MyLib", "*.csproj", SearchOption.TopDirectoryOnly).Returns([]);

        var context = MakeContext("/proj/MyLib");
        var project = new DotNetWrapperEmitter(fs).Emit(EmptySurface("/proj/MyLib"), context);

        var csproj = project.Files.Single(f => f.RelativePath == "TestServer.csproj").Content;
        Assert.Contains("MyLib.csproj", csproj);
    }

    [Fact]
    public void Emit_HttpTransport_FileSystemNotQueriedForTransportLogic()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.csproj", SearchOption.TopDirectoryOnly).Returns([]);

        var context = MakeContext("/src", Transport.StreamableHttp);
        var project = new DotNetWrapperEmitter(fs).Emit(EmptySurface("/src"), context);

        var csproj = project.Files.Single(f => f.RelativePath == "TestServer.csproj").Content;
        Assert.Contains("Microsoft.NET.Sdk.Web", csproj);
    }

    [Fact]
    public void Emit_SourceDirectoryWithCsproj_ReferencesFoundCsproj()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/proj/src").Returns(false);
        fs.DirectoryExists("/proj/src").Returns(true);
        fs.GetFiles("/proj/src", "*.csproj", SearchOption.TopDirectoryOnly)
            .Returns(["/proj/src/MyLib.csproj"]);

        var context = MakeContext("/proj/src");
        var project = new DotNetWrapperEmitter(fs).Emit(EmptySurface("/proj/src"), context);

        var csproj = project.Files.Single(f => f.RelativePath == "TestServer.csproj").Content;
        Assert.Contains("MyLib.csproj", csproj);
    }

    [Fact]
    public void Emit_SourceFileWithSiblingCsproj_ReferencesFoundCsproj()
    {
        var fs = Substitute.For<IFileSystem>();
        var dir3 = Path.GetDirectoryName("/proj/src/Class.cs")!;
        fs.FileExists("/proj/src/Class.cs").Returns(true);
        fs.DirectoryExists(dir3).Returns(true);
        fs.GetFiles(dir3, "*.csproj", SearchOption.TopDirectoryOnly)
            .Returns([Path.Combine(dir3, "MyLib.csproj")]);

        var context = MakeContext("/proj/src/Class.cs");
        var project = new DotNetWrapperEmitter(fs).Emit(EmptySurface("/proj/src/Class.cs"), context);

        var csproj = project.Files.Single(f => f.RelativePath == "TestServer.csproj").Content;
        Assert.Contains("MyLib.csproj", csproj);
    }

    [Fact]
    public void Emit_SourceDirectoryWithoutCsproj_UsesConventionalName()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/proj/MyLib").Returns(false);
        fs.DirectoryExists("/proj/MyLib").Returns(true);
        fs.GetFiles("/proj/MyLib", "*.csproj", SearchOption.TopDirectoryOnly).Returns([]);

        var context = MakeContext("/proj/MyLib");
        var project = new DotNetWrapperEmitter(fs).Emit(EmptySurface("/proj/MyLib"), context);

        var csproj = project.Files.Single(f => f.RelativePath == "TestServer.csproj").Content;
        Assert.Contains("MyLib.csproj", csproj);
    }

    [Fact]
    public void Emit_CsprojSourcePath_UsesItDirectly()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/proj/MyLib.csproj").Returns(true);
        fs.DirectoryExists("/proj").Returns(true);

        var context = MakeContext("/proj/MyLib.csproj");
        var project = new DotNetWrapperEmitter(fs).Emit(EmptySurface("/proj/MyLib.csproj"), context);

        var csproj = project.Files.Single(f => f.RelativePath == "TestServer.csproj").Content;
        Assert.Contains("MyLib.csproj", csproj);
    }
}
