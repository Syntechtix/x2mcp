using X2Mcp.Core.Abstractions;
using X2Mcp.Language.DotNet;

namespace X2Mcp.Language.DotNet.Tests;

public class RoslynScannerFileSystemTests
{
    private const string SimpleClassSource = """
        namespace Fixtures;
        public class Calculator
        {
            public int Add(int a, int b) => a + b;
        }
        """;

    [Fact]
    public void Scan_SingleFile_ReadsContentFromFileSystem()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("/src/Calc.cs").Returns(false);
        fs.ReadAllText("/src/Calc.cs").Returns(SimpleClassSource);

        var surface = new RoslynScanner(fs).Scan("/src/Calc.cs");

        fs.Received(1).ReadAllText("/src/Calc.cs");
        Assert.Equal("/src/Calc.cs", surface.SourcePath);
        Assert.Equal("csharp", surface.Language);
        Assert.Single(surface.Types);
        Assert.Equal("Calculator", surface.Types[0].Name);
    }

    [Fact]
    public void Scan_Directory_ListsCsFilesViaFileSystem()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.cs", SearchOption.AllDirectories)
            .Returns(["/src/Calc.cs"]);
        fs.ReadAllText("/src/Calc.cs").Returns(SimpleClassSource);

        var surface = new RoslynScanner(fs).Scan("/src");

        fs.Received(1).GetFiles("/src", "*.cs", SearchOption.AllDirectories);
        fs.Received(1).ReadAllText("/src/Calc.cs");
        Assert.Single(surface.Types);
    }

    [Fact]
    public void Scan_EmptyDirectory_ReturnsNoTypes()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("/empty").Returns(true);
        fs.GetFiles("/empty", "*.cs", SearchOption.AllDirectories).Returns([]);

        var surface = new RoslynScanner(fs).Scan("/empty");

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_InternalClass_ReturnsNoTypes()
    {
        const string internalSource = "namespace Ns; internal class Hidden { public void M() {} }";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("/src/H.cs").Returns(false);
        fs.ReadAllText("/src/H.cs").Returns(internalSource);

        var surface = new RoslynScanner(fs).Scan("/src/H.cs");

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_PublicClassNoPublicMethods_ReturnsNoTypes()
    {
        const string source = "public class NoPublic { private void Secret() {} }";
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("/src/N.cs").Returns(false);
        fs.ReadAllText("/src/N.cs").Returns(source);

        var surface = new RoslynScanner(fs).Scan("/src/N.cs");

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_AsyncMethod_IsDetectedCorrectly()
    {
        const string source = """
            public class Svc
            {
                public async System.Threading.Tasks.Task RunAsync() {}
            }
            """;
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("/src/Svc.cs").Returns(false);
        fs.ReadAllText("/src/Svc.cs").Returns(source);

        var surface = new RoslynScanner(fs).Scan("/src/Svc.cs");

        Assert.Single(surface.Types);
        Assert.True(surface.Types[0].Functions[0].IsAsync);
    }
}
