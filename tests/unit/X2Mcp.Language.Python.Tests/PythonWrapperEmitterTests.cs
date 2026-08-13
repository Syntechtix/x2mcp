using X2Mcp.Core.Models;
using X2Mcp.Language.Python;

namespace X2Mcp.Language.Python.Tests;

public class PythonWrapperEmitterTests
{
    private static BuildContext MakeContext(
        string sourcePath,
        Transport transport = Transport.Stdio,
        string serverName = "TestServer") =>
        new(sourcePath, "/out", $"/gen/{serverName}", serverName, transport);

    private static ScannedSurface MakeSurface(params TypeDescriptor[] types) =>
        new("/fake/source", "python", types);

    [Fact]
    public void Emit_IncludesMainPyAndCopiedSourceFile_ForSingleFileInput()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/lib.py").Returns(true);
        fs.ReadAllText("/src/lib.py").Returns("def add(a, b):\n    return a + b\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var project = new PythonWrapperEmitter(fs).Emit(surface, MakeContext("/src/lib.py"));

        Assert.Equal(2, project.Files.Count);
        Assert.Contains(project.Files, f => f.RelativePath == "main.py");
        Assert.Contains(project.Files, f => f.RelativePath == "lib.py");
    }

    [Fact]
    public void Emit_DirectoryInput_CopiesRelativePythonFiles()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.py", SearchOption.AllDirectories).Returns([
            "/src/lib.py",
            "/src/pkg/__init__.py",
            "/src/pkg/nested.py",
            "/src/test_helper.py",
        ]);
        fs.ReadAllText("/src/lib.py").Returns("x = 1\n");
        fs.ReadAllText("/src/pkg/__init__.py").Returns("\n");
        fs.ReadAllText("/src/pkg/nested.py").Returns("y = 2\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var project = new PythonWrapperEmitter(fs).Emit(surface, MakeContext("/src"));

        Assert.Contains(project.Files, f => f.RelativePath == "lib.py");
        Assert.Contains(project.Files, f => f.RelativePath == Path.Combine("pkg", "__init__.py"));
        Assert.Contains(project.Files, f => f.RelativePath == Path.Combine("pkg", "nested.py"));
        Assert.DoesNotContain(project.Files, f => f.RelativePath == "test_helper.py");
    }

    [Fact]
    public void Emit_ExistingNonPythonFile_ProducesOnlyMainPy()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/readme.txt").Returns(true);

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var project = new PythonWrapperEmitter(fs).Emit(surface, MakeContext("/src/readme.txt"));

        Assert.Single(project.Files);
        Assert.Equal("main.py", project.Files[0].RelativePath);
    }

    [Fact]
    public void Emit_NonExistentSourcePath_ProducesOnlyMainPy()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/missing").Returns(false);
        fs.DirectoryExists("/src/missing").Returns(false);

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var project = new PythonWrapperEmitter(fs).Emit(surface, MakeContext("/src/missing"));

        Assert.Single(project.Files);
        Assert.Equal("main.py", project.Files[0].RelativePath);
    }

    [Fact]
    public void Emit_MainPy_ImportsDistinctModules()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/lib.py").Returns(true);
        fs.ReadAllText("/src/lib.py").Returns("\n");

        var surface = MakeSurface(
            new TypeDescriptor("lib", "lib", [new FunctionDescriptor("add", [], "int", false)]),
            new TypeDescriptor("pkg.nested", "pkg.nested", [new FunctionDescriptor("greet", [], "str", false)]));

        var main = new PythonWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.py"))
            .Files.Single(f => f.RelativePath == "main.py").Content;

        Assert.Contains("import lib", main);
        Assert.Contains("import pkg.nested", main);
    }

    [Fact]
    public void Emit_MainPy_RegistersModuleFunctions()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/lib.py").Returns(true);
        fs.ReadAllText("/src/lib.py").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", [
            new FunctionDescriptor("add", [new ParameterDescriptor("a", "int", false)], "int", false),
            new FunctionDescriptor("fetch", [], "str", true),
        ]));

        var main = new PythonWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.py"))
            .Files.Single(f => f.RelativePath == "main.py").Content;

        Assert.Contains("mcp.tool(name=\"add\")(lib.add)", main);
        Assert.Contains("mcp.tool(name=\"fetch\")(lib.fetch)", main);
    }

    [Fact]
    public void Emit_MainPy_RegistersClassMethodsUsingInstance()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/lib.py").Returns(true);
        fs.ReadAllText("/src/lib.py").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "Calculator", [
            new FunctionDescriptor("add", [], "int", false),
            new FunctionDescriptor("fetch", [], "str", true),
        ]));

        var main = new PythonWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.py"))
            .Files.Single(f => f.RelativePath == "main.py").Content;

        Assert.Contains("_lib_Calculator = lib.Calculator()", main);
        Assert.Contains("mcp.tool(name=\"add\")(_lib_Calculator.add)", main);
        Assert.Contains("mcp.tool(name=\"fetch\")(_lib_Calculator.fetch)", main);
    }

    [Fact]
    public void Emit_MainPy_SanitizesInstanceNameForDottedModule()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/lib.py").Returns(true);
        fs.ReadAllText("/src/lib.py").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("pkg.nested", "Calculator", [
            new FunctionDescriptor("run", [], "int", false),
        ]));

        var main = new PythonWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.py"))
            .Files.Single(f => f.RelativePath == "main.py").Content;

        Assert.Contains("_pkg_nested_Calculator = pkg.nested.Calculator()", main);
    }

    [Fact]
    public void Emit_StdioTransport_UsesStdioRun()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/lib.py").Returns(true);
        fs.ReadAllText("/src/lib.py").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var main = new PythonWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.py", Transport.Stdio))
            .Files.Single(f => f.RelativePath == "main.py").Content;

        Assert.Contains("mcp.run(transport=\"stdio\")", main);
        Assert.DoesNotContain("streamable-http", main);
    }

    [Fact]
    public void Emit_HttpTransport_UsesStreamableHttpRun()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/lib.py").Returns(true);
        fs.ReadAllText("/src/lib.py").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var main = new PythonWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.py", Transport.StreamableHttp))
            .Files.Single(f => f.RelativePath == "main.py").Content;

        Assert.Contains("mcp.run(transport=\"streamable-http\")", main);
    }

    [Fact]
    public void Emit_ServerName_IsUsedInFastMcpInitialization()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/lib.py").Returns(true);
        fs.ReadAllText("/src/lib.py").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var main = new PythonWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.py", serverName: "PyServer"))
            .Files.Single(f => f.RelativePath == "main.py").Content;

        Assert.Contains("mcp = FastMCP(\"PyServer\")", main);
    }

    [Fact]
    public void Emit_ProjectPath_MatchesGeneratedProjectPath()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/lib.py").Returns(true);
        fs.ReadAllText("/src/lib.py").Returns("\n");

        var context = MakeContext("/src/lib.py");
        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));

        var project = new PythonWrapperEmitter(fs).Emit(surface, context);

        Assert.Equal(context.GeneratedProjectPath, project.ProjectPath);
    }
}
