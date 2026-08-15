using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;
using X2Mcp.Language.Go;

namespace X2Mcp.Language.Go.Tests;

public class GoWrapperEmitterTests
{
    private static IFileSystem MakeSourceModuleFs(string moduleName = "example.com/mylib")
    {
        var fs = Substitute.For<IFileSystem>();
        var goModPath = Path.Combine("/src", "go.mod");
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.FileExists(goModPath).Returns(true);
        fs.ReadAllText(goModPath).Returns($"module {moduleName}\n\ngo 1.23\n");
        return fs;
    }

    private static BuildContext MakeContext(
        string sourcePath,
        Transport transport = Transport.Stdio,
        string serverName = "TestServer") =>
        new(sourcePath, "/out", $"/gen/{serverName}", serverName, transport);

    private static ScannedSurface MakeSurface(params TypeDescriptor[] types) =>
        new("/fake/source", "go", types);

    [Fact]
    public void Emit_ReturnsGoModAndMainGo()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src");

        var project = new GoWrapperEmitter(fs).Emit(surface, context);

        Assert.Equal(2, project.Files.Count);
        Assert.Single(project.Files, f => f.RelativePath == "go.mod");
        Assert.Single(project.Files, f => f.RelativePath == "main.go");
    }

    [Fact]
    public void Emit_GoMod_ContainsModuleAndRequireLines()
    {
        var fs = MakeSourceModuleFs("example.com/mylib");
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src", serverName: "MyServer");

        var goMod = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "go.mod").Content;

        Assert.Contains("module x2mcp/generated/MyServer", goMod);
        Assert.Contains("github.com/modelcontextprotocol/go-sdk v1.7.0", goMod);
        Assert.Contains("example.com/mylib v0.0.0", goMod);
        Assert.Contains("replace example.com/mylib =>", goMod);
    }

    [Fact]
    public void Emit_SourceInSubdirectory_ComputesNestedImportPath()
    {
        var fs = Substitute.For<IFileSystem>();
        // Mirror the emitter's Path.GetDirectoryName/Combine calls to match on Windows
        var sourceDir = "/src/internal/fixtures";
        var parent1 = Path.GetDirectoryName(sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!;
        var moduleRoot = Path.GetDirectoryName(parent1.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!;
        var goModPath = Path.Combine(moduleRoot, "go.mod");
        fs.FileExists(Path.Combine(sourceDir, "go.mod")).Returns(false);
        fs.FileExists(Path.Combine(parent1, "go.mod")).Returns(false);
        fs.FileExists(goModPath).Returns(true);
        fs.ReadAllText(goModPath).Returns("module example.com/mylib\n\ngo 1.23\n");
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src/internal/fixtures");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("srcpkg \"example.com/mylib/internal/fixtures\"", mainGo);
    }

    [Fact]
    public void Emit_SourceIsFile_UsesParentDirectoryForImportPath()
    {
        var fs = Substitute.For<IFileSystem>();
        var sourceFile = "/src/internal/fixtures/lib.go";
        fs.FileExists(sourceFile).Returns(true);
        // Emitter calls Path.GetDirectoryName on file paths; mirror those calls for mock paths
        var fileDir = Path.GetDirectoryName(sourceFile)!;
        var parent1 = Path.GetDirectoryName(fileDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!;
        var moduleRoot = Path.GetDirectoryName(parent1.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!;
        var goModPath = Path.Combine(moduleRoot, "go.mod");
        fs.FileExists(Path.Combine(fileDir, "go.mod")).Returns(false);
        fs.FileExists(Path.Combine(parent1, "go.mod")).Returns(false);
        fs.FileExists(goModPath).Returns(true);
        fs.ReadAllText(goModPath).Returns("module example.com/mylib\n\ngo 1.23\n");

        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src/internal/fixtures/lib.go");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("srcpkg \"example.com/mylib/internal/fixtures\"", mainGo);
    }

    [Fact]
    public void Emit_SourceAtModuleRoot_UsesModulePathDirectly()
    {
        var fs = MakeSourceModuleFs("example.com/mylib");
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("srcpkg \"example.com/mylib\"", mainGo);
    }

    [Fact]
    public void Emit_NoReturnFunction_GeneratesFireAndForgetCall()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("LogMessage", [new ParameterDescriptor("msg", "string", false)], "", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("srcpkg.LogMessage(args.Msg)", mainGo);
        Assert.Contains("return nil, nil, nil", mainGo);
    }

    [Fact]
    public void Emit_ValueOnlyFunction_ReturnsResultAsOutput()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Greet", [new ParameterDescriptor("name", "string", false)], "string", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("result := srcpkg.Greet(args.Name)", mainGo);
        Assert.Contains("return nil, result, nil", mainGo);
    }

    [Fact]
    public void Emit_MapReturnType_TreatsBracketedTypeAsSingleValue()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("GetMap", [], "map[string]int", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("result := srcpkg.GetMap()", mainGo);
        Assert.Contains("return nil, result, nil", mainGo);
    }

    [Fact]
    public void Emit_ErrorOnlyFunction_ChecksAndPropagatesError()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Validate", [new ParameterDescriptor("input", "string", false)], "error", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("err := srcpkg.Validate(args.Input)", mainGo);
        Assert.Contains("if err != nil {", mainGo);
    }

    [Fact]
    public void Emit_ValueAndErrorFunction_ReturnsResultAndPropagatesError()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Divide", [
                new ParameterDescriptor("a", "float64", false),
                new ParameterDescriptor("b", "float64", false),
            ], "(float64, error)", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("result, err := srcpkg.Divide(args.A, args.B)", mainGo);
        Assert.Contains("return nil, result, nil", mainGo);
        Assert.Contains("return nil, nil, err", mainGo);
    }

    [Fact]
    public void Emit_UnsupportedReturnShape_ThrowsNotSupportedException()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Weird", [], "(int, string, error)", false),
        ]));
        var context = MakeContext("/src");

        Assert.Throws<NotSupportedException>(() => new GoWrapperEmitter(fs).Emit(surface, context));
    }

    [Fact]
    public void Emit_Function_GeneratesArgsStructWithJsonTags()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Add", [
                new ParameterDescriptor("a", "int", false),
                new ParameterDescriptor("b", "int", false),
            ], "int", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("type pkgAddArgs struct {", mainGo);
        Assert.Contains("A int `json:\"a\"`", mainGo);
        Assert.Contains("B int `json:\"b\"`", mainGo);
    }

    [Fact]
    public void Emit_StdioTransport_GeneratesStdioRun()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src", Transport.Stdio);

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("mcp.StdioTransport{}", mainGo);
        Assert.DoesNotContain("NewStreamableHTTPHandler", mainGo);
    }

    [Fact]
    public void Emit_HttpTransport_GeneratesStatelessStreamableHandler()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src", Transport.StreamableHttp);

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("NewStreamableHTTPHandler", mainGo);
        Assert.Contains("Stateless: true", mainGo);
        Assert.Contains("\"net/http\"", mainGo);
    }

    [Fact]
    public void Emit_ServerName_UsedInImplementation()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src", serverName: "MyCoolServer");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("Name: \"MyCoolServer\"", mainGo);
    }

    [Fact]
    public void Emit_MultipleFunctions_GeneratesOneRegistrationEach()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Add", [], "int", false),
            new FunctionDescriptor("Greet", [], "string", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("Name: \"Add\"", mainGo);
        Assert.Contains("Name: \"Greet\"", mainGo);
    }

    [Fact]
    public void Emit_ReceiverMethods_GeneratesReceiverInstanceAndMethodCalls()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("fixtures", "Calculator", [
            new FunctionDescriptor("Add", [
                new ParameterDescriptor("a", "int", false),
                new ParameterDescriptor("b", "int", false),
            ], "int", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("receiver0_Calculator := new(srcpkg.Calculator)", mainGo);
        Assert.Contains("result := receiver0_Calculator.Add(args.A, args.B)", mainGo);
        Assert.Contains("Name: \"Calculator_Add\"", mainGo);
    }

    [Fact]
    public void Emit_MethodAndTopLevelSameName_UsesDistinctArgsTypes()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(
            new TypeDescriptor("", "fixtures", [
                new FunctionDescriptor("Add", [new ParameterDescriptor("a", "int", false)], "int", false),
            ]),
            new TypeDescriptor("fixtures", "Calculator", [
                new FunctionDescriptor("Add", [new ParameterDescriptor("a", "int", false)], "int", false),
            ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("type pkgAddArgs struct {", mainGo);
        Assert.Contains("type CalculatorAddArgs struct {", mainGo);
        Assert.Contains("Name: \"Add\"", mainGo);
        Assert.Contains("Name: \"Calculator_Add\"", mainGo);
    }

    [Fact]
    public void Emit_MethodWithUnderscoreReceiver_CoversIdentifierSanitization()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("fixtures", "Calculator_V2", [
            new FunctionDescriptor("Add", [new ParameterDescriptor("a", "int", false)], "int", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("type Calculator_V2AddArgs struct {", mainGo);
        Assert.Contains("receiver0_Calculator_V2 := new(srcpkg.Calculator_V2)", mainGo);
        Assert.Contains("Name: \"Calculator_V2_Add\"", mainGo);
    }

    [Fact]
    public void Emit_TwoDifferentReceiverTypes_GeneratesBothInstancesOnSeparateLines()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(
            new TypeDescriptor("fixtures", "Calculator", [
                new FunctionDescriptor("Add", [], "int", false),
            ]),
            new TypeDescriptor("fixtures", "Logger", [
                new FunctionDescriptor("Log", [], "", false),
            ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("new(srcpkg.Calculator)", mainGo);
        Assert.Contains("new(srcpkg.Logger)", mainGo);
    }

    [Fact]
    public void Emit_ReturnTypeStartsWithParenButDoesNotEndWithOne_TreatedAsValueOnly()
    {
        // Realistic source: a trailing comment after the closing paren but before the function's
        // opening brace (e.g. `func F() (int) // note {`) leaves the captured return type ending
        // in the comment text rather than ')'.
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Annotated", [], "(int) // note", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("result := srcpkg.Annotated()", mainGo);
        Assert.Contains("return nil, result, nil", mainGo);
    }

    [Fact]
    public void Emit_ReturnTypeWithCommaNestedInsideBrackets_DoesNotSplitOnNestedComma()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Lookup", [], "(Pair[int, string], error)", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("result, err := srcpkg.Lookup()", mainGo);
        Assert.Contains("return nil, result, nil", mainGo);
        Assert.Contains("return nil, nil, err", mainGo);
    }

    [Fact]
    public void Emit_ReturnTypeWithCommaNestedInsideParens_DoesNotSplitOnNestedComma()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Apply", [], "(func(int, int) int, error)", false),
        ]));
        var context = MakeContext("/src");

        var mainGo = new GoWrapperEmitter(fs).Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("result, err := srcpkg.Apply()", mainGo);
        Assert.Contains("return nil, result, nil", mainGo);
        Assert.Contains("return nil, nil, err", mainGo);
    }

    [Fact]
    public void Emit_NoSourceModuleFound_ThrowsInvalidOperationException()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/orphan").Returns(false);
        fs.DirectoryExists("/orphan").Returns(true);
        fs.FileExists("/orphan/go.mod").Returns(false);

        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/orphan");

        Assert.Throws<InvalidOperationException>(() => new GoWrapperEmitter(fs).Emit(surface, context));
    }

    [Fact]
    public void Emit_GoModWithoutModuleDirective_ThrowsInvalidOperationException()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.FileExists("/src/go.mod").Returns(true);
        fs.ReadAllText("/src/go.mod").Returns("go 1.23\n");

        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src");

        Assert.Throws<InvalidOperationException>(() => new GoWrapperEmitter(fs).Emit(surface, context));
    }

    [Fact]
    public void Emit_ProjectPath_MatchesContextGeneratedProjectPath()
    {
        var fs = MakeSourceModuleFs();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext("/src");

        var project = new GoWrapperEmitter(fs).Emit(surface, context);

        Assert.Equal(context.GeneratedProjectPath, project.ProjectPath);
    }
}
