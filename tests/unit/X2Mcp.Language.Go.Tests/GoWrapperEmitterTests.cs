using X2Mcp.Core.Models;
using X2Mcp.Language.Go;

namespace X2Mcp.Language.Go.Tests;

public class GoWrapperEmitterTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"x2mcp-go-emit-{Guid.NewGuid():N}");

    public GoWrapperEmitterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string MakeSourceModule(string moduleName = "example.com/mylib", string subDir = "")
    {
        var moduleDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, "go.mod"), $"module {moduleName}\n\ngo 1.23\n");

        var sourceDir = subDir.Length == 0 ? moduleDir : Path.Combine(moduleDir, subDir);
        Directory.CreateDirectory(sourceDir);
        return sourceDir;
    }

    private BuildContext MakeContext(
        string sourcePath,
        Transport transport = Transport.Stdio,
        string serverName = "TestServer") =>
        new(sourcePath,
            Path.Combine(_tempDir, "out"),
            Path.Combine(_tempDir, "gen", serverName),
            serverName,
            transport);

    private static ScannedSurface MakeSurface(params TypeDescriptor[] types) =>
        new("/fake/source", "go", types);

    [Fact]
    public void Emit_ReturnsGoModAndMainGo()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceDir);

        var project = new GoWrapperEmitter().Emit(surface, context);

        Assert.Equal(2, project.Files.Count);
        Assert.Single(project.Files, f => f.RelativePath == "go.mod");
        Assert.Single(project.Files, f => f.RelativePath == "main.go");
    }

    [Fact]
    public void Emit_GoMod_ContainsModuleAndRequireLines()
    {
        var sourceDir = MakeSourceModule("example.com/mylib");
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceDir, serverName: "MyServer");

        var goMod = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "go.mod").Content;

        Assert.Contains("module x2mcp/generated/MyServer", goMod);
        Assert.Contains("github.com/modelcontextprotocol/go-sdk v1.7.0", goMod);
        Assert.Contains("example.com/mylib v0.0.0", goMod);
        Assert.Contains("replace example.com/mylib =>", goMod);
    }

    [Fact]
    public void Emit_SourceInSubdirectory_ComputesNestedImportPath()
    {
        var sourceDir = MakeSourceModule("example.com/mylib", "internal/fixtures");
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("srcpkg \"example.com/mylib/internal/fixtures\"", mainGo);
    }

    [Fact]
    public void Emit_SourceIsFile_UsesParentDirectoryForImportPath()
    {
        var sourceDir = MakeSourceModule("example.com/mylib", "internal/fixtures");
        var sourceFile = Path.Combine(sourceDir, "lib.go");
        File.WriteAllText(sourceFile, "package fixtures\n");

        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceFile);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("srcpkg \"example.com/mylib/internal/fixtures\"", mainGo);
    }

    [Fact]
    public void Emit_SourceAtModuleRoot_UsesModulePathDirectly()
    {
        var sourceDir = MakeSourceModule("example.com/mylib");
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("srcpkg \"example.com/mylib\"", mainGo);
    }

    [Fact]
    public void Emit_NoReturnFunction_GeneratesFireAndForgetCall()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("LogMessage", [new ParameterDescriptor("msg", "string", false)], "", false),
        ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("srcpkg.LogMessage(args.Msg)", mainGo);
        Assert.Contains("return nil, nil, nil", mainGo);
    }

    [Fact]
    public void Emit_ValueOnlyFunction_ReturnsResultAsOutput()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Greet", [new ParameterDescriptor("name", "string", false)], "string", false),
        ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("result := srcpkg.Greet(args.Name)", mainGo);
        Assert.Contains("return nil, result, nil", mainGo);
    }

    [Fact]
    public void Emit_MapReturnType_TreatsBracketedTypeAsSingleValue()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("GetMap", [], "map[string]int", false),
        ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("result := srcpkg.GetMap()", mainGo);
        Assert.Contains("return nil, result, nil", mainGo);
    }

    [Fact]
    public void Emit_ErrorOnlyFunction_ChecksAndPropagatesError()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Validate", [new ParameterDescriptor("input", "string", false)], "error", false),
        ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("err := srcpkg.Validate(args.Input)", mainGo);
        Assert.Contains("if err != nil {", mainGo);
    }

    [Fact]
    public void Emit_ValueAndErrorFunction_ReturnsResultAndPropagatesError()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Divide", [
                new ParameterDescriptor("a", "float64", false),
                new ParameterDescriptor("b", "float64", false),
            ], "(float64, error)", false),
        ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("result, err := srcpkg.Divide(args.A, args.B)", mainGo);
        Assert.Contains("return nil, result, nil", mainGo);
        Assert.Contains("return nil, nil, err", mainGo);
    }

    [Fact]
    public void Emit_UnsupportedReturnShape_ThrowsNotSupportedException()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Weird", [], "(int, string, error)", false),
        ]));
        var context = MakeContext(sourceDir);

        Assert.Throws<NotSupportedException>(() => new GoWrapperEmitter().Emit(surface, context));
    }

    [Fact]
    public void Emit_Function_GeneratesArgsStructWithJsonTags()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Add", [
                new ParameterDescriptor("a", "int", false),
                new ParameterDescriptor("b", "int", false),
            ], "int", false),
        ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("type pkgAddArgs struct {", mainGo);
        Assert.Contains("A int `json:\"a\"`", mainGo);
        Assert.Contains("B int `json:\"b\"`", mainGo);
    }

    [Fact]
    public void Emit_StdioTransport_GeneratesStdioRun()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceDir, Transport.Stdio);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("mcp.StdioTransport{}", mainGo);
        Assert.DoesNotContain("NewStreamableHTTPHandler", mainGo);
    }

    [Fact]
    public void Emit_HttpTransport_GeneratesStatelessStreamableHandler()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceDir, Transport.StreamableHttp);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("NewStreamableHTTPHandler", mainGo);
        Assert.Contains("Stateless: true", mainGo);
        Assert.Contains("\"net/http\"", mainGo);
    }

    [Fact]
    public void Emit_ServerName_UsedInImplementation()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceDir, serverName: "MyCoolServer");

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("Name: \"MyCoolServer\"", mainGo);
    }

    [Fact]
    public void Emit_MultipleFunctions_GeneratesOneRegistrationEach()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", [
            new FunctionDescriptor("Add", [], "int", false),
            new FunctionDescriptor("Greet", [], "string", false),
        ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("Name: \"Add\"", mainGo);
        Assert.Contains("Name: \"Greet\"", mainGo);
    }

    [Fact]
    public void Emit_ReceiverMethods_GeneratesReceiverInstanceAndMethodCalls()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("fixtures", "Calculator", [
            new FunctionDescriptor("Add", [
                new ParameterDescriptor("a", "int", false),
                new ParameterDescriptor("b", "int", false),
            ], "int", false),
        ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("receiver0_Calculator := new(srcpkg.Calculator)", mainGo);
        Assert.Contains("result := receiver0_Calculator.Add(args.A, args.B)", mainGo);
        Assert.Contains("Name: \"Calculator_Add\"", mainGo);
    }

    [Fact]
    public void Emit_MethodAndTopLevelSameName_UsesDistinctArgsTypes()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(
            new TypeDescriptor("", "fixtures", [
                new FunctionDescriptor("Add", [new ParameterDescriptor("a", "int", false)], "int", false),
            ]),
            new TypeDescriptor("fixtures", "Calculator", [
                new FunctionDescriptor("Add", [new ParameterDescriptor("a", "int", false)], "int", false),
            ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("type pkgAddArgs struct {", mainGo);
        Assert.Contains("type CalculatorAddArgs struct {", mainGo);
        Assert.Contains("Name: \"Add\"", mainGo);
        Assert.Contains("Name: \"Calculator_Add\"", mainGo);
    }

    [Fact]
    public void Emit_MethodWithUnderscoreReceiver_CoversIdentifierSanitization()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("fixtures", "Calculator_V2", [
            new FunctionDescriptor("Add", [new ParameterDescriptor("a", "int", false)], "int", false),
        ]));
        var context = MakeContext(sourceDir);

        var mainGo = new GoWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "main.go").Content;

        Assert.Contains("type Calculator_V2AddArgs struct {", mainGo);
        Assert.Contains("receiver0_Calculator_V2 := new(srcpkg.Calculator_V2)", mainGo);
        Assert.Contains("Name: \"Calculator_V2_Add\"", mainGo);
    }

    [Fact]
    public void Emit_NoSourceModuleFound_ThrowsInvalidOperationException()
    {
        var sourceDir = Path.Combine(_tempDir, "orphan");
        Directory.CreateDirectory(sourceDir);
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceDir);

        Assert.Throws<InvalidOperationException>(() => new GoWrapperEmitter().Emit(surface, context));
    }

    [Fact]
    public void Emit_GoModWithoutModuleDirective_ThrowsInvalidOperationException()
    {
        var moduleDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, "go.mod"), "go 1.23\n");

        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(moduleDir);

        Assert.Throws<InvalidOperationException>(() => new GoWrapperEmitter().Emit(surface, context));
    }

    [Fact]
    public void Emit_ProjectPath_MatchesContextGeneratedProjectPath()
    {
        var sourceDir = MakeSourceModule();
        var surface = MakeSurface(new TypeDescriptor("", "fixtures", []));
        var context = MakeContext(sourceDir);

        var project = new GoWrapperEmitter().Emit(surface, context);

        Assert.Equal(context.GeneratedProjectPath, project.ProjectPath);
    }
}
