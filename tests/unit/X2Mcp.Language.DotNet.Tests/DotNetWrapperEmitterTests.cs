using X2Mcp.Core.Models;
using X2Mcp.Language.DotNet;

namespace X2Mcp.Language.DotNet.Tests;

public class DotNetWrapperEmitterTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"x2mcp-emit-{Guid.NewGuid():N}");

    public DotNetWrapperEmitterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
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

    [Fact]
    public void Emit_SingleType_GeneratesCsprojProgramAndToolsFile()
    {
        var surface = MakeSurface(
            new TypeDescriptor("MyLib", "Calculator", [
                new FunctionDescriptor("Add", [
                    new ParameterDescriptor("a", "int", false),
                    new ParameterDescriptor("b", "int", false),
                ], "int", false),
            ]));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"));

        var project = new DotNetWrapperEmitter().Emit(surface, context);

        Assert.Equal(3, project.Files.Count); // csproj + Program.cs + CalculatorTools.cs
        Assert.Single(project.Files, f => f.RelativePath == "McpServer.csproj");
        Assert.Single(project.Files, f => f.RelativePath == "Program.cs");
        Assert.Single(project.Files, f => f.RelativePath == "CalculatorTools.cs");
    }

    [Fact]
    public void Emit_Csproj_ContainsRequiredElements()
    {
        var surface = MakeSurface(new TypeDescriptor("MyLib", "Svc", []));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"));

        var project = new DotNetWrapperEmitter().Emit(surface, context);
        var csproj = project.Files.Single(f => f.RelativePath == "McpServer.csproj").Content;

        Assert.Contains("net10.0", csproj);
        Assert.Contains("ModelContextProtocol", csproj);
        Assert.Contains("ProjectReference", csproj);
        Assert.Contains("Microsoft.NET.Sdk", csproj);
    }

    [Fact]
    public void Emit_StdioTransport_GeneratesStdioSetup()
    {
        var surface = MakeSurface(new TypeDescriptor("", "Svc", []));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"), Transport.Stdio);

        var programCs = new DotNetWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "Program.cs").Content;

        Assert.Contains("WithStdioServerTransport", programCs);
        Assert.DoesNotContain("MapMcp", programCs);
        Assert.DoesNotContain("WithHttpTransport", programCs);
    }

    [Fact]
    public void Emit_HttpTransport_GeneratesAspNetCoreSetup()
    {
        var surface = MakeSurface(new TypeDescriptor("", "Svc", []));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"), Transport.StreamableHttp);

        var project = new DotNetWrapperEmitter().Emit(surface, context);
        var programCs = project.Files.Single(f => f.RelativePath == "Program.cs").Content;
        var csproj = project.Files.Single(f => f.RelativePath == "McpServer.csproj").Content;

        Assert.Contains("MapMcp", programCs);
        Assert.Contains("WithHttpTransport", programCs);
        Assert.Contains("options.Stateless = true", programCs);
        Assert.DoesNotContain("WithStdioServerTransport", programCs);
        Assert.Contains("Microsoft.NET.Sdk.Web", csproj);
    }

    [Fact]
    public void Emit_TypeWithNamespace_InjectsUsingInProgram()
    {
        var surface = MakeSurface(new TypeDescriptor("My.Namespace", "Svc", []));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"));

        var programCs = new DotNetWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "Program.cs").Content;

        Assert.Contains("using My.Namespace;", programCs);
    }

    [Fact]
    public void Emit_TypeWithEmptyNamespace_NoExtraUsingInProgram()
    {
        var surface = MakeSurface(new TypeDescriptor("", "Svc", []));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"));

        var programCs = new DotNetWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "Program.cs").Content;

        Assert.DoesNotContain("using ;", programCs);
    }

    [Fact]
    public void Emit_AsyncMethod_GeneratesAsyncDelegation()
    {
        var surface = MakeSurface(new TypeDescriptor("Lib", "Svc", [
            new FunctionDescriptor("RunAsync", [], "Task<string>", true),
        ]));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"));

        var toolsCs = new DotNetWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "SvcTools.cs").Content;

        Assert.Contains("async ", toolsCs);
        Assert.Contains("await ", toolsCs);
    }

    [Fact]
    public void Emit_SyncMethod_GeneratesSyncDelegation()
    {
        var surface = MakeSurface(new TypeDescriptor("Lib", "Svc", [
            new FunctionDescriptor("Run", [], "string", false),
        ]));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"));

        var toolsCs = new DotNetWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "SvcTools.cs").Content;

        Assert.DoesNotContain("async ", toolsCs);
        Assert.DoesNotContain("await ", toolsCs);
    }

    [Fact]
    public void Emit_TypeWithMultipleMethods_SeparatesMethodsWithBlankLine()
    {
        var surface = MakeSurface(
            new TypeDescriptor("Lib", "Calculator", [
                new FunctionDescriptor("Add", [], "int", false),
                new FunctionDescriptor("Subtract", [], "int", false),
            ]));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"));

        var toolsCs = new DotNetWrapperEmitter().Emit(surface, context)
            .Files.Single(f => f.RelativePath == "CalculatorTools.cs").Content;

        Assert.Contains("Add", toolsCs);
        Assert.Contains("Subtract", toolsCs);
    }

    [Fact]
    public void Emit_MultipleTypes_GeneratesOneToolsFilePerType()
    {
        var surface = MakeSurface(
            new TypeDescriptor("Lib", "Alpha", []),
            new TypeDescriptor("Lib", "Beta", []));
        var context = MakeContext(Path.Combine(_tempDir, "FakeSource"));

        var project = new DotNetWrapperEmitter().Emit(surface, context);

        Assert.Single(project.Files, f => f.RelativePath == "AlphaTools.cs");
        Assert.Single(project.Files, f => f.RelativePath == "BetaTools.cs");
    }

    [Fact]
    public void Emit_ProjectPath_MatchesContextGeneratedProjectPath()
    {
        var surface = MakeSurface(new TypeDescriptor("", "Svc", []));
        var context = MakeContext(Path.Combine(_tempDir, "Fake"));

        var project = new DotNetWrapperEmitter().Emit(surface, context);

        Assert.Equal(context.GeneratedProjectPath, project.ProjectPath);
    }

    private static ScannedSurface MakeSurface(params TypeDescriptor[] types) =>
        new("/fake/source", "csharp", types);
}
