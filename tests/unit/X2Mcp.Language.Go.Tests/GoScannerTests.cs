using X2Mcp.Core.Abstractions;
using X2Mcp.Language.Go;

namespace X2Mcp.Language.Go.Tests;

public class GoScannerTests
{
    private const string PublicFunctionsSource = """
        package fixtures

        func Add(a, b int) int {
            return a + b
        }

        func Greet(name string) string {
            return "Hello, " + name
        }

        func LogMessage(msg string) {
            println(msg)
        }

        func Validate(input string) error {
            return nil
        }

        func Divide(a, b float64) (float64, error) {
            return a / b, nil
        }
        """;

    private const string UnexportedSource = """
        package fixtures

        func unexportedFunc() int {
            return 0
        }
        """;

    private const string MethodsSource = """
        package fixtures

        type Calculator struct{}
        type CalculatorGeneric[T any] struct{}

        func (c Calculator) Add(a, b int) int {
            return a + b
        }

        func (c *Calculator) Multiply(a, b int) int {
            return a * b
        }

        func (c CalculatorGeneric[T]) Generic(a int) int {
            return a
        }

        func (c Calculator) hidden() int {
            return 0
        }

        type internalCalculator struct{}

        func (c internalCalculator) PublicButInternalType() int {
            return 1
        }
        """;

    private static IFileSystem FileAt(string path, string content)
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.ReadAllText(path).Returns(content);
        return fs;
    }

    [Fact]
    public void Scan_PublicFunctions_ReturnsGoLanguage()
    {
        var surface = new GoScanner(FileAt("/src/public_functions.go", PublicFunctionsSource))
            .Scan("/src/public_functions.go");
        Assert.Equal("go", surface.Language);
    }

    [Fact]
    public void Scan_PublicFunctions_GroupsIntoSinglePackageType()
    {
        var surface = new GoScanner(FileAt("/src/public_functions.go", PublicFunctionsSource))
            .Scan("/src/public_functions.go");
        Assert.Single(surface.Types);
        Assert.Equal("fixtures", surface.Types[0].Name);
    }

    [Fact]
    public void Scan_PublicFunctions_HasFiveExportedFunctions()
    {
        var surface = new GoScanner(FileAt("/src/public_functions.go", PublicFunctionsSource))
            .Scan("/src/public_functions.go");
        Assert.Equal(5, surface.Types[0].Functions.Count);
    }

    [Fact]
    public void Scan_AddFunction_HasGroupedParameters()
    {
        var surface = new GoScanner(FileAt("/src/public_functions.go", PublicFunctionsSource))
            .Scan("/src/public_functions.go");
        var add = surface.Types[0].Functions.Single(f => f.Name == "Add");

        Assert.Equal(2, add.Parameters.Count);
        Assert.Equal("a", add.Parameters[0].Name);
        Assert.Equal("int", add.Parameters[0].Type);
        Assert.Equal("b", add.Parameters[1].Name);
        Assert.Equal("int", add.Parameters[1].Type);
        Assert.Equal("int", add.ReturnType);
        Assert.False(add.IsAsync);
    }

    [Fact]
    public void Scan_LogMessage_HasNoReturnType()
    {
        var surface = new GoScanner(FileAt("/src/public_functions.go", PublicFunctionsSource))
            .Scan("/src/public_functions.go");
        Assert.Equal("", surface.Types[0].Functions.Single(f => f.Name == "LogMessage").ReturnType);
    }

    [Fact]
    public void Scan_Validate_HasErrorReturnType()
    {
        var surface = new GoScanner(FileAt("/src/public_functions.go", PublicFunctionsSource))
            .Scan("/src/public_functions.go");
        Assert.Equal("error", surface.Types[0].Functions.Single(f => f.Name == "Validate").ReturnType);
    }

    [Fact]
    public void Scan_Divide_HasValueAndErrorReturnType()
    {
        var surface = new GoScanner(FileAt("/src/public_functions.go", PublicFunctionsSource))
            .Scan("/src/public_functions.go");
        Assert.Equal("(float64, error)", surface.Types[0].Functions.Single(f => f.Name == "Divide").ReturnType);
    }

    [Fact]
    public void Scan_UnexportedFile_ReturnsNoFunctions()
    {
        var surface = new GoScanner(FileAt("/src/unexported.go", UnexportedSource))
            .Scan("/src/unexported.go");
        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_ReceiverMethods_ExcludesMethodsFromTopLevelFunctions()
    {
        var surface = new GoScanner(FileAt("/src/methods.go", MethodsSource))
            .Scan("/src/methods.go");
        Assert.Equal(2, surface.Types.Count);

        var calc = surface.Types.Single(t => t.Name == "Calculator");
        Assert.Equal("fixtures", calc.Namespace);
        Assert.Equal(2, calc.Functions.Count);
        Assert.Contains(calc.Functions, f => f.Name == "Add");
        Assert.Contains(calc.Functions, f => f.Name == "Multiply");
        Assert.DoesNotContain(calc.Functions, f => f.Name == "hidden");
        Assert.DoesNotContain(calc.Functions, f => f.Name == "PublicButInternalType");

        var generic = surface.Types.Single(t => t.Name == "CalculatorGeneric");
        Assert.Equal("fixtures", generic.Namespace);
        Assert.Single(generic.Functions);
        Assert.Equal("Generic", generic.Functions[0].Name);
    }

    [Fact]
    public void Scan_Directory_ExcludesTestFiles()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.go", SearchOption.AllDirectories)
            .Returns(["/src/public_functions.go", "/src/methods.go", "/src/sample_test.go"]);
        fs.ReadAllText("/src/public_functions.go").Returns(PublicFunctionsSource);
        fs.ReadAllText("/src/methods.go").Returns(MethodsSource);

        var surface = new GoScanner(fs).Scan("/src");

        Assert.Equal(3, surface.Types.Count);
        Assert.Single(surface.Types, t => t.Namespace == "" && t.Name == "fixtures");
        Assert.Single(surface.Types, t => t.Namespace == "fixtures" && t.Name == "Calculator");
        Assert.Single(surface.Types, t => t.Namespace == "fixtures" && t.Name == "CalculatorGeneric");
    }

    [Fact]
    public void Scan_NonExistentPath_ReturnsNoTypes()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/does-not-exist").Returns(false);
        fs.DirectoryExists("/does-not-exist").Returns(false);

        var surface = new GoScanner(fs).Scan("/does-not-exist");
        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_SourcePath_IsPreserved()
    {
        var surface = new GoScanner(FileAt("/src/public_functions.go", PublicFunctionsSource))
            .Scan("/src/public_functions.go");
        Assert.Equal("/src/public_functions.go", surface.SourcePath);
    }

    [Fact]
    public void IsExported_EmptyString_ReturnsFalse() =>
        Assert.False(GoScanner.IsExported(""));

    [Fact]
    public void Scan_MethodWithPointerOnlyReceiver_Ignored()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("input.go").Returns(true);
        fs.ReadAllText("input.go").Returns("""
            package fixtures

            func (c *) InvalidReceiver() int {
                return 0
            }
            """);

        var surface = new GoScanner(fs).Scan("input.go");
        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_ZeroParameterFunction_HasNoParameters()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("input.go").Returns(true);
        fs.ReadAllText("input.go").Returns("""
            package fixtures

            func Ping() string {
                return "pong"
            }
            """);

        var surface = new GoScanner(fs).Scan("input.go");
        Assert.Empty(surface.Types[0].Functions.Single(f => f.Name == "Ping").Parameters);
    }

    [Fact]
    public void Scan_MethodWithEmptyReceiver_Ignored()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("input.go").Returns(true);
        fs.ReadAllText("input.go").Returns("""
            package fixtures

            func () InvalidReceiver() int {
                return 0
            }
            """);

        var surface = new GoScanner(fs).Scan("input.go");
        Assert.Empty(surface.Types);
    }
}
