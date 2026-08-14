using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;
using X2Mcp.Language.Rust;

namespace X2Mcp.Language.Rust.Tests;

public class RustWrapperEmitterFileSystemTests
{
    private static BuildContext MakeContext(
        string sourcePath,
        Transport transport = Transport.Stdio,
        string serverName = "TestServer") =>
        new(sourcePath, "/out", $"/gen/{serverName}", serverName, transport);

    private static ScannedSurface EmptySurface(string sourcePath = "/fake/src") =>
        new(sourcePath, "rust", []);

    [Fact]
    public void Emit_SourceIsDirectory_FindsCargoTomlInSameDirectory()
    {
        var fs = Substitute.For<IFileSystem>();
        var cargoTomlPath = Path.Combine("/proj/src", "Cargo.toml");
        fs.FileExists("/proj/src").Returns(false);
        fs.FileExists(cargoTomlPath).Returns(true);
        fs.ReadAllText(cargoTomlPath).Returns("[package]\nname = \"lib\"\n");

        var context = MakeContext("/proj/src");
        var project = new RustWrapperEmitter(fs).Emit(EmptySurface("/proj/src"), context);

        var cargoToml = project.Files.Single(f => f.RelativePath == "Cargo.toml").Content;
        Assert.Contains("lib = { path =", cargoToml);
    }

    [Fact]
    public void Emit_SourceIsFile_ResolvesParentDirectoryForCargoToml()
    {
        var fs = Substitute.For<IFileSystem>();
        var sourceFile = Path.Combine("/proj/src", "lib.rs");
        fs.FileExists(sourceFile).Returns(true);
        var fileDir = Path.GetDirectoryName(sourceFile)!;
        var cargoTomlPath = Path.Combine(fileDir, "Cargo.toml");
        fs.FileExists(cargoTomlPath).Returns(true);
        fs.ReadAllText(cargoTomlPath).Returns("[package]\nname = \"lib\"\n");

        var context = MakeContext(sourceFile);
        var project = new RustWrapperEmitter(fs).Emit(EmptySurface(sourceFile), context);

        var cargoToml = project.Files.Single(f => f.RelativePath == "Cargo.toml").Content;
        Assert.Contains("lib = { path =", cargoToml);
    }

    [Fact]
    public void Emit_CargoTomlInParentDirectory_WalksUpDirectoryTree()
    {
        var fs = Substitute.For<IFileSystem>();
        var sourceDir = Path.Combine("/proj/src", "pkg");
        fs.FileExists(sourceDir).Returns(false);
        fs.FileExists(Path.Combine(sourceDir, "Cargo.toml")).Returns(false);
        var parent = Path.GetDirectoryName(sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!;
        var cargoTomlPath = Path.Combine(parent, "Cargo.toml");
        fs.FileExists(cargoTomlPath).Returns(true);
        fs.ReadAllText(cargoTomlPath).Returns("[package]\nname = \"lib\"\n");

        var context = MakeContext(sourceDir);
        var project = new RustWrapperEmitter(fs).Emit(EmptySurface(sourceDir), context);

        var cargoToml = project.Files.Single(f => f.RelativePath == "Cargo.toml").Content;
        Assert.Contains("lib = { path =", cargoToml);
    }

    [Fact]
    public void Emit_NoCargoTomlAnywhereUpTheTree_ThrowsInvalidOperationException()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(Arg.Any<string>()).Returns(false);

        var sourceDir = Path.Combine("/proj/src", "pkg");
        var context = MakeContext(sourceDir);

        Assert.Throws<InvalidOperationException>(
            () => new RustWrapperEmitter(fs).Emit(EmptySurface(sourceDir), context));
    }

    [Fact]
    public void Emit_HttpTransport_FileSystemNotQueriedForTransportLogic()
    {
        var fs = Substitute.For<IFileSystem>();
        var cargoTomlPath = Path.Combine("/src", "Cargo.toml");
        fs.FileExists("/src").Returns(false);
        fs.FileExists(cargoTomlPath).Returns(true);
        fs.ReadAllText(cargoTomlPath).Returns("[package]\nname = \"lib\"\n");

        var context = MakeContext("/src", Transport.StreamableHttp);
        var project = new RustWrapperEmitter(fs).Emit(EmptySurface("/src"), context);

        var mainRs = project.Files.Single(f => f.RelativePath == "src/main.rs").Content;
        Assert.Contains("StreamableHttpService::new", mainRs);
    }

    [Fact]
    public void Emit_CargoTomlWithNameCommentedOutsidePackageSection_ThrowsInvalidOperationException()
    {
        var fs = Substitute.For<IFileSystem>();
        var cargoTomlPath = Path.Combine("/src", "Cargo.toml");
        fs.FileExists("/src").Returns(false);
        fs.FileExists(cargoTomlPath).Returns(true);
        fs.ReadAllText(cargoTomlPath).Returns("[dependencies]\nname = \"not-the-package-name\"\n");

        var context = MakeContext("/src");

        Assert.Throws<InvalidOperationException>(
            () => new RustWrapperEmitter(fs).Emit(EmptySurface("/src"), context));
    }
}
