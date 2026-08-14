using X2Mcp.Core.Abstractions;
using X2Mcp.Language.Python;

namespace X2Mcp.Language.Python.Tests;

public class PythonScannerTests
{
    private const string PublicFunctionsSource = """
        def add(a: int, b: int) -> int:
            return a + b


        def greet(first: str, last: str = "") -> str:
            return f"Hello, {first} {last}".strip()


        def ping() -> None:
            return None


        async def fetch(url: str) -> str:
            return url


        def variadic(*items: int) -> list[int]:
            return list(items)


        def transform(payload: dict[str, list[int]] = {"a": [1, 2]}) -> dict[str, list[int]]:
            return payload
        """;

    private const string ClassMethodsSource = """
        class Calculator:
            def __init__(self) -> None:
                self.factor = 1

            def add(self, a: int, b: int) -> int:
                return a + b

            async def fetch(self, value: str) -> str:
                return value

            def _private(self) -> int:
                return 0
        """;

    private const string UnexportedSource = """
        def _hidden(value: int) -> int:
            return value
        """;

    private const string NestedFunctionsSource = """
        def outer(value: int) -> int:
            def inner(hidden: int) -> int:
                return hidden

            return value
        """;

    private const string TabIndentedSource = """
        class Tabbed:
        	def tabbed(self, value: int) -> int:
        		return value
        """;

    private const string NestedModuleSource = """
        def echo(value: str) -> str:
            return value
        """;

    private static IFileSystem FileAt(string path, string content)
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.ReadAllText(path).Returns(content);
        return fs;
    }

    [Fact]
    public void Scan_PublicFunctions_ReturnsPythonLanguage()
    {
        var surface = new PythonScanner(FileAt("/src/public_functions.py", PublicFunctionsSource))
            .Scan("/src/public_functions.py");

        Assert.Equal("python", surface.Language);
    }

    [Fact]
    public void Scan_PublicFunctions_GroupsIntoSingleModuleType()
    {
        var surface = new PythonScanner(FileAt("/src/public_functions.py", PublicFunctionsSource))
            .Scan("/src/public_functions.py");

        Assert.Single(surface.Types);
        Assert.Equal("public_functions", surface.Types[0].Name);
        Assert.Equal("public_functions", surface.Types[0].Namespace);
    }

    [Fact]
    public void Scan_PublicFunctions_HasFiveExportedFunctions()
    {
        var surface = new PythonScanner(FileAt("/src/public_functions.py", PublicFunctionsSource))
            .Scan("/src/public_functions.py");

        Assert.Equal(6, surface.Types[0].Functions.Count);
    }

    [Fact]
    public void Scan_Add_HasTypedParametersAndReturnType()
    {
        var surface = new PythonScanner(FileAt("/src/public_functions.py", PublicFunctionsSource))
            .Scan("/src/public_functions.py");
        var add = surface.Types[0].Functions.Single(f => f.Name == "add");

        Assert.Equal(2, add.Parameters.Count);
        Assert.Equal("a", add.Parameters[0].Name);
        Assert.Equal("int", add.Parameters[0].Type);
        Assert.False(add.Parameters[0].IsOptional);
        Assert.Equal("b", add.Parameters[1].Name);
        Assert.Equal("int", add.Parameters[1].Type);
        Assert.False(add.Parameters[1].IsOptional);
        Assert.Equal("int", add.ReturnType);
        Assert.False(add.IsAsync);
    }

    [Fact]
    public void Scan_Greet_MarksDefaultArgumentOptional()
    {
        var surface = new PythonScanner(FileAt("/src/public_functions.py", PublicFunctionsSource))
            .Scan("/src/public_functions.py");

        Assert.True(surface.Types[0].Functions.Single(f => f.Name == "greet").Parameters[1].IsOptional);
    }

    [Fact]
    public void Scan_Fetch_IsAsync()
    {
        var surface = new PythonScanner(FileAt("/src/public_functions.py", PublicFunctionsSource))
            .Scan("/src/public_functions.py");
        var fetch = surface.Types[0].Functions.Single(f => f.Name == "fetch");

        Assert.True(fetch.IsAsync);
        Assert.Equal("str", fetch.ReturnType);
    }

    [Fact]
    public void Scan_ClassMethods_CapturesOnlyPublicMethods()
    {
        var surface = new PythonScanner(FileAt("/src/class_methods.py", ClassMethodsSource))
            .Scan("/src/class_methods.py");

        Assert.Single(surface.Types);
        Assert.Equal("Calculator", surface.Types[0].Name);
        Assert.Equal(2, surface.Types[0].Functions.Count);
        Assert.Contains(surface.Types[0].Functions, f => f.Name == "add");
        Assert.Contains(surface.Types[0].Functions, f => f.Name == "fetch");
        Assert.DoesNotContain(surface.Types[0].Functions, f => f.Name == "_private");
        Assert.DoesNotContain(surface.Types[0].Functions, f => f.Name == "__init__");
    }

    [Fact]
    public void Scan_ClassMethods_ExcludesSelfParameter()
    {
        var surface = new PythonScanner(FileAt("/src/class_methods.py", ClassMethodsSource))
            .Scan("/src/class_methods.py");
        var add = surface.Types[0].Functions.Single(f => f.Name == "add");

        Assert.Equal(2, add.Parameters.Count);
        Assert.Equal("a", add.Parameters[0].Name);
        Assert.Equal("b", add.Parameters[1].Name);
    }

    [Fact]
    public void Scan_UnexportedFile_ReturnsNoFunctions()
    {
        var surface = new PythonScanner(FileAt("/src/unexported.py", UnexportedSource))
            .Scan("/src/unexported.py");

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_Directory_ExcludesTestFilesAndBuildsNestedModuleNames()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.py", SearchOption.AllDirectories).Returns([
            "/src/public_functions.py",
            "/src/class_methods.py",
            "/src/test_sample.py",
            "/src/pkg/nested.py",
        ]);
        fs.ReadAllText("/src/public_functions.py").Returns(PublicFunctionsSource);
        fs.ReadAllText("/src/class_methods.py").Returns(ClassMethodsSource);
        fs.ReadAllText("/src/pkg/nested.py").Returns(NestedModuleSource);

        var surface = new PythonScanner(fs).Scan("/src");

        var namespaces = surface.Types.Select(t => t.Namespace).ToList();
        Assert.Contains("public_functions", namespaces);
        Assert.Contains("class_methods", namespaces);
        Assert.Contains("pkg.nested", namespaces);
        Assert.DoesNotContain("test_sample", namespaces);
    }

    [Fact]
    public void Scan_Directory_PackageInitFile_StripsDunderInitFromModuleName()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.py", SearchOption.AllDirectories).Returns([
            "/src/pkg/__init__.py",
        ]);
        fs.ReadAllText("/src/pkg/__init__.py").Returns(NestedModuleSource);

        var surface = new PythonScanner(fs).Scan("/src");

        var namespaces = surface.Types.Select(t => t.Namespace).ToList();
        Assert.Contains("pkg", namespaces);
        Assert.DoesNotContain("pkg.__init__", namespaces);
    }

    [Fact]
    public void Scan_NonExistentPath_ReturnsNoTypes()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/does-not-exist").Returns(false);
        fs.DirectoryExists("/does-not-exist").Returns(false);

        var surface = new PythonScanner(fs).Scan("/does-not-exist");

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_ExistingNonPythonFile_ReturnsNoTypes()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/readme.txt").Returns(true);

        var surface = new PythonScanner(fs).Scan("/src/readme.txt");

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_NestedFunctions_ExcludesInnerFunction()
    {
        var surface = new PythonScanner(FileAt("/src/nested_functions.py", NestedFunctionsSource))
            .Scan("/src/nested_functions.py");

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("outer", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_TabIndentedClass_ParsesMethods()
    {
        var surface = new PythonScanner(FileAt("/src/tab_indented_class.py", TabIndentedSource))
            .Scan("/src/tab_indented_class.py");

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("tabbed", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_PublicFunctions_ComplexTypeParameterPreservesAnnotation()
    {
        var surface = new PythonScanner(FileAt("/src/public_functions.py", PublicFunctionsSource))
            .Scan("/src/public_functions.py");
        var complex = surface.Types[0].Functions.Single(f => f.Name == "transform");

        Assert.Single(complex.Parameters);
        Assert.Equal("dict[str, list[int]]", complex.Parameters[0].Type);
        Assert.True(complex.Parameters[0].IsOptional);
    }

    [Fact]
    public void Scan_SourcePath_IsPreserved()
    {
        var surface = new PythonScanner(FileAt("/src/public_functions.py", PublicFunctionsSource))
            .Scan("/src/public_functions.py");

        Assert.Equal("/src/public_functions.py", surface.SourcePath);
    }
}
