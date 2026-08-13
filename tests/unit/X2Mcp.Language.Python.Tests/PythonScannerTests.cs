using X2Mcp.Language.Python;

namespace X2Mcp.Language.Python.Tests;

public class PythonScannerTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Scan_PublicFunctions_ReturnsPythonLanguage()
    {
        var surface = new PythonScanner().Scan(Fixture("public_functions.py"));

        Assert.Equal("python", surface.Language);
    }

    [Fact]
    public void Scan_PublicFunctions_GroupsIntoSingleModuleType()
    {
        var surface = new PythonScanner().Scan(Fixture("public_functions.py"));

        Assert.Single(surface.Types);
        Assert.Equal("public_functions", surface.Types[0].Name);
        Assert.Equal("public_functions", surface.Types[0].Namespace);
    }

    [Fact]
    public void Scan_PublicFunctions_HasFiveExportedFunctions()
    {
        var surface = new PythonScanner().Scan(Fixture("public_functions.py"));

        Assert.Equal(6, surface.Types[0].Functions.Count);
    }

    [Fact]
    public void Scan_Add_HasTypedParametersAndReturnType()
    {
        var surface = new PythonScanner().Scan(Fixture("public_functions.py"));
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
        var surface = new PythonScanner().Scan(Fixture("public_functions.py"));
        var greet = surface.Types[0].Functions.Single(f => f.Name == "greet");

        Assert.Equal(2, greet.Parameters.Count);
        Assert.True(greet.Parameters[1].IsOptional);
    }

    [Fact]
    public void Scan_Fetch_IsAsync()
    {
        var surface = new PythonScanner().Scan(Fixture("public_functions.py"));
        var fetch = surface.Types[0].Functions.Single(f => f.Name == "fetch");

        Assert.True(fetch.IsAsync);
        Assert.Equal("str", fetch.ReturnType);
    }

    [Fact]
    public void Scan_ClassMethods_CapturesOnlyPublicMethods()
    {
        var surface = new PythonScanner().Scan(Fixture("class_methods.py"));

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
        var surface = new PythonScanner().Scan(Fixture("class_methods.py"));
        var add = surface.Types[0].Functions.Single(f => f.Name == "add");

        Assert.Equal(2, add.Parameters.Count);
        Assert.Equal("a", add.Parameters[0].Name);
        Assert.Equal("b", add.Parameters[1].Name);
    }

    [Fact]
    public void Scan_UnexportedFile_ReturnsNoFunctions()
    {
        var surface = new PythonScanner().Scan(Fixture("unexported.py"));

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_Directory_ExcludesTestFilesAndBuildsNestedModuleNames()
    {
        var dir = Path.GetDirectoryName(Fixture("public_functions.py"))!;
        var surface = new PythonScanner().Scan(dir);

        var names = surface.Types.Select(t => t.Namespace).ToList();
        Assert.Contains("public_functions", names);
        Assert.Contains("class_methods", names);
        Assert.Contains("pkg.nested", names);
        Assert.DoesNotContain("test_sample", names);
    }

    [Fact]
    public void Scan_NonExistentPath_ReturnsNoTypes()
    {
        var surface = new PythonScanner().Scan(Path.Combine(AppContext.BaseDirectory, "does-not-exist"));

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_ExistingNonPythonFile_ReturnsNoTypes()
    {
        var fs = Substitute.For<X2Mcp.Core.Abstractions.IFileSystem>();
        fs.FileExists("/src/readme.txt").Returns(true);

        var surface = new PythonScanner(fs).Scan("/src/readme.txt");

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_NestedFunctions_ExcludesInnerFunction()
    {
        var surface = new PythonScanner().Scan(Fixture("nested_functions.py"));

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("outer", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_TabIndentedClass_ParsesMethods()
    {
        var surface = new PythonScanner().Scan(Fixture("tab_indented_class.py"));

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("tabbed", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_PublicFunctions_ComplexTypeParameterPreservesAnnotation()
    {
        var surface = new PythonScanner().Scan(Fixture("public_functions.py"));
        var complex = surface.Types[0].Functions.Single(f => f.Name == "transform");

        Assert.Single(complex.Parameters);
        Assert.Equal("dict[str, list[int]]", complex.Parameters[0].Type);
        Assert.True(complex.Parameters[0].IsOptional);
    }

    [Fact]
    public void Scan_SourcePath_IsPreserved()
    {
        var path = Fixture("public_functions.py");
        var surface = new PythonScanner().Scan(path);

        Assert.Equal(path, surface.SourcePath);
    }
}
