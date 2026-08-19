using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;
using X2Mcp.Language.Rust;

namespace X2Mcp.Language.Rust.Tests;

public class RustWrapperEmitterTests
{
    private static IFileSystem MakeSourceCrateFs(string crateName = "mylib")
    {
        var fs = Substitute.For<IFileSystem>();
        var cargoTomlPath = Path.Combine("/src", "Cargo.toml");
        fs.FileExists("/src").Returns(false);
        fs.FileExists(cargoTomlPath).Returns(true);
        fs.ReadAllText(cargoTomlPath).Returns($"[package]\nname = \"{crateName}\"\nversion = \"0.1.0\"\nedition = \"2021\"\n");
        return fs;
    }

    private static BuildContext MakeContext(
        string sourcePath,
        Transport transport = Transport.Stdio,
        string serverName = "TestServer") =>
        new(sourcePath, "/out", $"/gen/{serverName}", serverName, transport);

    private static ScannedSurface MakeSurface(params TypeDescriptor[] types) =>
        new("/fake/source", "rust", types);

    [Fact]
    public void Emit_ReturnsCargoTomlAndMainRs()
    {
        var fs = MakeSourceCrateFs();
        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/src");

        var project = new RustWrapperEmitter(fs).Emit(surface, context);

        Assert.Equal(2, project.Files.Count);
        Assert.Single(project.Files, f => f.RelativePath == "Cargo.toml");
        Assert.Single(project.Files, f => f.RelativePath == "src/main.rs");
    }

    [Fact]
    public void Emit_CargoToml_ContainsPathDependencyAndBinName()
    {
        var fs = MakeSourceCrateFs("mylib");
        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/src", serverName: "MyServer");

        var cargoToml = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "Cargo.toml").Content;

        Assert.Contains("name = \"MyServer\"", cargoToml);
        Assert.Contains("mylib = { path =", cargoToml);
        Assert.Contains("rmcp = {", cargoToml);
    }

    [Fact]
    public void Emit_ServerNameMatchesSourceCrateName_PackageNameDoesNotCollide()
    {
        // Regression test: when --name matches the source crate's own package name (exactly what
        // docs/examples/rust.md's own worked example does — a crate named "calculator" wrapped
        // with --name calculator), the wrapper's [package] name used to equal the source crate's
        // name too, and `cargo install` failed with "package collision in the lockfile" since two
        // different packages can't share a name+version in one build graph. The [package] name
        // must now always be distinct from the source crate name, while [[bin]] stays exactly the
        // requested server name (that's the name the built binary actually gets on disk).
        var fs = MakeSourceCrateFs("calculator");
        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/src", serverName: "calculator");

        var cargoToml = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "Cargo.toml").Content;

        Assert.Contains("[[bin]]\nname = \"calculator\"", cargoToml);
        Assert.DoesNotContain("[package]\nname = \"calculator\"", cargoToml);
        Assert.Contains("calculator = { path =", cargoToml);
    }

    [Fact]
    public void Emit_BinNameStartingWithDigit_IsPrefixedWithUnderscore()
    {
        var fs = MakeSourceCrateFs();
        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/src", serverName: "1server");

        var cargoToml = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "Cargo.toml").Content;

        Assert.Contains("name = \"_1server\"", cargoToml);
    }

    [Fact]
    public void Emit_BinNameWithHyphenUnderscoreAndInvalidChar_SanitizesEachDifferently()
    {
        var fs = MakeSourceCrateFs();
        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/src", serverName: "1My-Name_Weird!Char");

        var cargoToml = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "Cargo.toml").Content;

        Assert.Contains("name = \"_1My-Name_Weird_Char\"", cargoToml);
    }

    [Fact]
    public void Emit_MultipleFunctionsAcrossTypes_SeparatesGeneratedBlocksWithBlankLines()
    {
        var fs = MakeSourceCrateFs("mylib");
        var surface = MakeSurface(
            new TypeDescriptor("", "functions", [
                new FunctionDescriptor("add", [], "i32", false),
            ]),
            new TypeDescriptor("", "Calculator", [
                new FunctionDescriptor("multiply", [], "i32", false),
            ]));
        var context = MakeContext("/src");

        var mainRs = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "src/main.rs").Content;

        Assert.Contains("struct AddParams {", mainRs);
        Assert.Contains("struct CalculatorMultiplyParams {", mainRs);
        Assert.Contains("description = \"add\"", mainRs);
        Assert.Contains("description = \"Calculator_multiply\"", mainRs);
    }

    [Fact]
    public void Emit_StdioTransport_GeneratesStdioSetupAndNoHttpDeps()
    {
        var fs = MakeSourceCrateFs();
        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/src", Transport.Stdio);

        var project = new RustWrapperEmitter(fs).Emit(surface, context);
        var mainRs = project.Files.Single(f => f.RelativePath == "src/main.rs").Content;
        var cargoToml = project.Files.Single(f => f.RelativePath == "Cargo.toml").Content;

        Assert.Contains("use rmcp::transport::stdio;", mainRs);
        Assert.Contains("GeneratedTools.serve(stdio())", mainRs);
        Assert.DoesNotContain("axum", cargoToml);
    }

    [Fact]
    public void Emit_HttpTransport_GeneratesStreamableHttpSetupAndAxumDep()
    {
        var fs = MakeSourceCrateFs();
        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/src", Transport.StreamableHttp);

        var project = new RustWrapperEmitter(fs).Emit(surface, context);
        var mainRs = project.Files.Single(f => f.RelativePath == "src/main.rs").Content;
        var cargoToml = project.Files.Single(f => f.RelativePath == "Cargo.toml").Content;

        Assert.Contains("StreamableHttpService::new", mainRs);
        Assert.Contains("axum::serve", mainRs);
        Assert.DoesNotContain("GeneratedTools.serve(stdio())", mainRs);
        Assert.Contains("axum = \"0.8\"", cargoToml);
    }

    [Fact]
    public void Emit_FreeFunction_GeneratesParamsStructAndDirectCall()
    {
        var fs = MakeSourceCrateFs("mylib");
        var surface = MakeSurface(new TypeDescriptor("", "functions", [
            new FunctionDescriptor("add", [
                new ParameterDescriptor("a", "i32", false),
                new ParameterDescriptor("b", "i32", false),
            ], "i32", false),
        ]));
        var context = MakeContext("/src");

        var mainRs = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "src/main.rs").Content;

        Assert.Contains("struct AddParams {", mainRs);
        Assert.Contains("a: i32,", mainRs);
        Assert.Contains("mylib::add(a, b)", mainRs);
        Assert.Contains("description = \"add\"", mainRs);
    }

    [Fact]
    public void Emit_HyphenatedCrateName_UsesUnderscoreInRustIdentifiersButHyphenInCargoToml()
    {
        var fs = MakeSourceCrateFs("my-lib");
        var surface = MakeSurface(new TypeDescriptor("", "functions", [
            new FunctionDescriptor("add", [], "i32", false),
        ]));
        var context = MakeContext("/src");

        var project = new RustWrapperEmitter(fs).Emit(surface, context);
        var mainRs = project.Files.Single(f => f.RelativePath == "src/main.rs").Content;
        var cargoToml = project.Files.Single(f => f.RelativePath == "Cargo.toml").Content;

        Assert.Contains("my-lib = { path =", cargoToml);
        Assert.Contains("use my_lib;", mainRs);
        Assert.Contains("my_lib::add()", mainRs);
    }

    [Fact]
    public void Emit_StructMethod_GeneratesDefaultInstanceCallAndPrefixedToolName()
    {
        var fs = MakeSourceCrateFs("mylib");
        var surface = MakeSurface(new TypeDescriptor("", "Calculator", [
            new FunctionDescriptor("add", [
                new ParameterDescriptor("a", "i32", false),
                new ParameterDescriptor("b", "i32", false),
            ], "i32", false),
        ]));
        var context = MakeContext("/src");

        var mainRs = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "src/main.rs").Content;

        Assert.Contains("struct CalculatorAddParams {", mainRs);
        Assert.Contains("mylib::Calculator::default().add(a, b)", mainRs);
        Assert.Contains("description = \"Calculator_add\"", mainRs);
    }

    [Fact]
    public void Emit_FunctionWithNoReturnType_ReturnsOkAcknowledgement()
    {
        var fs = MakeSourceCrateFs("mylib");
        var surface = MakeSurface(new TypeDescriptor("", "functions", [
            new FunctionDescriptor("log_message", [
                new ParameterDescriptor("msg", "String", false),
            ], "", false),
        ]));
        var context = MakeContext("/src");

        var mainRs = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "src/main.rs").Content;

        Assert.Contains("mylib::log_message(msg);", mainRs);
        Assert.Contains("\"ok\".to_string()", mainRs);
    }

    [Fact]
    public void Emit_FunctionWithReturnType_FormatsResultWithDebug()
    {
        var fs = MakeSourceCrateFs("mylib");
        var surface = MakeSurface(new TypeDescriptor("", "functions", [
            new FunctionDescriptor("greet", [
                new ParameterDescriptor("name", "String", false),
            ], "String", false),
        ]));
        var context = MakeContext("/src");

        var mainRs = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "src/main.rs").Content;

        Assert.Contains("let result = mylib::greet(name);", mainRs);
        Assert.Contains("format!(\"{:?}\", result)", mainRs);
    }

    [Fact]
    public void Emit_AsyncFunction_GeneratesAsyncAwaitCall()
    {
        var fs = MakeSourceCrateFs("mylib");
        var surface = MakeSurface(new TypeDescriptor("", "functions", [
            new FunctionDescriptor("fetch", [
                new ParameterDescriptor("id", "i32", false),
            ], "String", true),
        ]));
        var context = MakeContext("/src");

        var mainRs = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "src/main.rs").Content;

        Assert.Contains("async fn fetch", mainRs);
        Assert.Contains("mylib::fetch(id).await", mainRs);
    }

    [Fact]
    public void Emit_SourceInSubdirectory_ComputesRelativeCratePath()
    {
        var fs = Substitute.For<IFileSystem>();
        var sourceDir = "/src/internal";
        var parent = Path.GetDirectoryName(sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!;
        fs.FileExists(sourceDir).Returns(false);
        fs.FileExists(Path.Combine(sourceDir, "Cargo.toml")).Returns(false);
        fs.FileExists(Path.Combine(parent, "Cargo.toml")).Returns(true);
        fs.ReadAllText(Path.Combine(parent, "Cargo.toml")).Returns("[package]\nname = \"mylib\"\nversion = \"0.1.0\"\n");

        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext(sourceDir);

        var cargoToml = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "Cargo.toml").Content;

        Assert.Contains("mylib = { path = \"../../src\" }", cargoToml);
    }

    [Fact]
    public void Emit_SourceIsFile_UsesParentDirectoryToFindCrate()
    {
        var fs = Substitute.For<IFileSystem>();
        var sourceFile = Path.Combine("/src", "lib.rs");
        fs.FileExists(sourceFile).Returns(true);
        var fileDir = Path.GetDirectoryName(sourceFile)!;
        fs.FileExists(Path.Combine(fileDir, "Cargo.toml")).Returns(true);
        fs.ReadAllText(Path.Combine(fileDir, "Cargo.toml")).Returns("[package]\nname = \"mylib\"\nversion = \"0.1.0\"\n");

        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext(sourceFile);

        var cargoToml = new RustWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "Cargo.toml").Content;

        Assert.Contains("mylib = { path =", cargoToml);
    }

    [Fact]
    public void Emit_NoCargoTomlFound_ThrowsInvalidOperationException()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(Arg.Any<string>()).Returns(false);

        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/orphan");

        Assert.Throws<InvalidOperationException>(() => new RustWrapperEmitter(fs).Emit(surface, context));
    }

    [Fact]
    public void Emit_CargoTomlWithoutPackageName_ThrowsInvalidOperationException()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.FileExists(Path.Combine("/src", "Cargo.toml")).Returns(true);
        fs.ReadAllText(Path.Combine("/src", "Cargo.toml")).Returns("[dependencies]\n");

        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/src");

        Assert.Throws<InvalidOperationException>(() => new RustWrapperEmitter(fs).Emit(surface, context));
    }

    [Fact]
    public void Emit_ProjectPath_MatchesContextGeneratedProjectPath()
    {
        var fs = MakeSourceCrateFs();
        var surface = MakeSurface(new TypeDescriptor("", "functions", []));
        var context = MakeContext("/src");

        var project = new RustWrapperEmitter(fs).Emit(surface, context);

        Assert.Equal(context.GeneratedProjectPath, project.ProjectPath);
    }
}
