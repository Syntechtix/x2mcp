using X2Mcp.Core.Abstractions;
using X2Mcp.Language.DotNet;

namespace X2Mcp.Language.DotNet.Tests;

public class RoslynScannerTests
{
    private const string PublicMethodsSource = """
        using System.Threading.Tasks;
        namespace Fixtures;
        public class Calculator
        {
            public int Add(int a, int b) => a + b;
            public int Subtract(int a, int b) => a - b;
            public async Task<double> DivideAsync(double numerator, double denominator)
            {
                await Task.Delay(0);
                return numerator / denominator;
            }
            public string Format(double value, string format = "G") => value.ToString(format);
        }
        public class Greeter
        {
            public string Greet(string name) => $"Hello, {name}!";
        }
        """;

    private const string InternalClassSource = """
        namespace Fixtures;
        internal class InternalService
        {
            public string Execute(string input) => input;
        }
        """;

    private const string PrivateMethodsSource = """
        namespace Fixtures;
        public class MixedAccess
        {
            public string PublicMethod(string input) => input;
            private string PrivateMethod(string input) => input;
            protected string ProtectedMethod(string input) => input;
            internal string InternalMethod(string input) => input;
        }
        """;

    private const string BlockNamespaceSource = """
        namespace Fixtures.Block
        {
            public class BlockScoped
            {
                public int DoubleIt(int value) => value * 2;
            }
        }
        """;

    private static IFileSystem FileAt(string path, string content)
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(path).Returns(false);
        fs.ReadAllText(path).Returns(content);
        return fs;
    }

    private static IFileSystem DirAt(string dir, params (string path, string content)[] files)
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists(dir).Returns(true);
        fs.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Returns(files.Select(f => f.path).ToArray());
        foreach (var (path, content) in files)
            fs.ReadAllText(path).Returns(content);
        return fs;
    }

    [Fact]
    public void Scan_PublicClassWithPublicMethods_ReturnsTypes()
    {
        var surface = new RoslynScanner(FileAt("/src/PublicMethods.cs", PublicMethodsSource))
            .Scan("/src/PublicMethods.cs");

        Assert.Equal("csharp", surface.Language);
        Assert.Equal(2, surface.Types.Count);
    }

    [Fact]
    public void Scan_Calculator_HasCorrectMethodCount()
    {
        var surface = new RoslynScanner(FileAt("/src/PublicMethods.cs", PublicMethodsSource))
            .Scan("/src/PublicMethods.cs");

        Assert.Equal(4, surface.Types.Single(t => t.Name == "Calculator").Functions.Count);
    }

    [Fact]
    public void Scan_Calculator_HasCorrectNamespace()
    {
        var surface = new RoslynScanner(FileAt("/src/PublicMethods.cs", PublicMethodsSource))
            .Scan("/src/PublicMethods.cs");

        Assert.Equal("Fixtures", surface.Types.Single(t => t.Name == "Calculator").Namespace);
    }

    [Fact]
    public void Scan_AddMethod_HasCorrectParameters()
    {
        var surface = new RoslynScanner(FileAt("/src/PublicMethods.cs", PublicMethodsSource))
            .Scan("/src/PublicMethods.cs");
        var add = surface.Types.Single(t => t.Name == "Calculator").Functions.Single(f => f.Name == "Add");

        Assert.Equal(2, add.Parameters.Count);
        Assert.Equal("a", add.Parameters[0].Name);
        Assert.Equal("int", add.Parameters[0].Type);
        Assert.False(add.Parameters[0].IsOptional);
        Assert.Equal("int", add.ReturnType);
        Assert.False(add.IsAsync);
    }

    [Fact]
    public void Scan_DivideAsync_IsDetectedAsAsync()
    {
        var surface = new RoslynScanner(FileAt("/src/PublicMethods.cs", PublicMethodsSource))
            .Scan("/src/PublicMethods.cs");

        Assert.True(surface.Types.Single(t => t.Name == "Calculator")
            .Functions.Single(f => f.Name == "DivideAsync").IsAsync);
    }

    [Fact]
    public void Scan_FormatMethod_HasOptionalParameter()
    {
        var surface = new RoslynScanner(FileAt("/src/PublicMethods.cs", PublicMethodsSource))
            .Scan("/src/PublicMethods.cs");

        Assert.True(surface.Types.Single(t => t.Name == "Calculator")
            .Functions.Single(f => f.Name == "Format")
            .Parameters.Single(p => p.Name == "format").IsOptional);
    }

    [Fact]
    public void Scan_InternalClass_ReturnsNoTypes()
    {
        var surface = new RoslynScanner(FileAt("/src/InternalClass.cs", InternalClassSource))
            .Scan("/src/InternalClass.cs");

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_PrivateMethods_ExcludesNonPublic()
    {
        var surface = new RoslynScanner(FileAt("/src/PrivateMethods.cs", PrivateMethodsSource))
            .Scan("/src/PrivateMethods.cs");

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("PublicMethod", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_Directory_ScansAllCsFiles()
    {
        var fs = DirAt("/src",
            ("/src/PublicMethods.cs", PublicMethodsSource),
            ("/src/PrivateMethods.cs", PrivateMethodsSource));

        var surface = new RoslynScanner(fs).Scan("/src");

        Assert.True(surface.Types.Count >= 3);
    }

    [Fact]
    public void Scan_SourcePath_IsPreserved()
    {
        var surface = new RoslynScanner(FileAt("/src/PublicMethods.cs", PublicMethodsSource))
            .Scan("/src/PublicMethods.cs");

        Assert.Equal("/src/PublicMethods.cs", surface.SourcePath);
    }

    [Fact]
    public void Scan_ClassInsideBlockScopedNamespace_ResolvesNamespace()
    {
        var surface = new RoslynScanner(FileAt("/src/BlockNamespace.cs", BlockNamespaceSource))
            .Scan("/src/BlockNamespace.cs");

        Assert.Equal("Fixtures.Block", surface.Types.Single(t => t.Name == "BlockScoped").Namespace);
    }

    [Fact]
    public void Scan_CsprojPath_ScansDirectoryFiles()
    {
        var scanRoot = Path.GetDirectoryName("/src/Calculator.csproj")!;
        var filePath = Path.Combine(scanRoot, "PublicMethods.cs");
        var fs = DirAt(scanRoot, (filePath, PublicMethodsSource));

        var surface = new RoslynScanner(fs).Scan("/src/Calculator.csproj");

        Assert.Equal(2, surface.Types.Count);
        fs.Received(1).GetFiles(scanRoot, "*.cs", SearchOption.AllDirectories);
    }

    [Fact]
    public void Scan_Directory_ExcludesBinAndObjFiles()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.cs", SearchOption.AllDirectories)
            .Returns(["/src/Calculator.cs", "/src/bin/Generated.cs", "/src/obj/Compiled.cs"]);
        fs.ReadAllText("/src/Calculator.cs")
            .Returns("public class Calculator { public int Add(int a, int b) => a + b; }");

        var surface = new RoslynScanner(fs).Scan("/src");

        Assert.Single(surface.Types);
        Assert.Equal("Calculator", surface.Types[0].Name);
        fs.DidNotReceive().ReadAllText("/src/bin/Generated.cs");
        fs.DidNotReceive().ReadAllText("/src/obj/Compiled.cs");
    }
}
