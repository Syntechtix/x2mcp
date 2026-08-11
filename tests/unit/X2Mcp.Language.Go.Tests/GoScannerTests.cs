using X2Mcp.Language.Go;

namespace X2Mcp.Language.Go.Tests;

public class GoScannerTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Scan_PublicFunctions_ReturnsGoLanguage()
    {
        var surface = new GoScanner().Scan(Fixture("public_functions.go"));
        Assert.Equal("go", surface.Language);
    }

    [Fact]
    public void Scan_PublicFunctions_GroupsIntoSinglePackageType()
    {
        var surface = new GoScanner().Scan(Fixture("public_functions.go"));
        Assert.Single(surface.Types);
        Assert.Equal("fixtures", surface.Types[0].Name);
    }

    [Fact]
    public void Scan_PublicFunctions_HasFiveExportedFunctions()
    {
        var surface = new GoScanner().Scan(Fixture("public_functions.go"));
        Assert.Equal(5, surface.Types[0].Functions.Count);
    }

    [Fact]
    public void Scan_AddFunction_HasGroupedParameters()
    {
        var surface = new GoScanner().Scan(Fixture("public_functions.go"));
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
        var surface = new GoScanner().Scan(Fixture("public_functions.go"));
        var log = surface.Types[0].Functions.Single(f => f.Name == "LogMessage");
        Assert.Equal("", log.ReturnType);
    }

    [Fact]
    public void Scan_Validate_HasErrorReturnType()
    {
        var surface = new GoScanner().Scan(Fixture("public_functions.go"));
        var validate = surface.Types[0].Functions.Single(f => f.Name == "Validate");
        Assert.Equal("error", validate.ReturnType);
    }

    [Fact]
    public void Scan_Divide_HasValueAndErrorReturnType()
    {
        var surface = new GoScanner().Scan(Fixture("public_functions.go"));
        var divide = surface.Types[0].Functions.Single(f => f.Name == "Divide");
        Assert.Equal("(float64, error)", divide.ReturnType);
    }

    [Fact]
    public void Scan_UnexportedFile_ReturnsNoFunctions()
    {
        var surface = new GoScanner().Scan(Fixture("unexported.go"));
        Assert.Single(surface.Types);
        Assert.Empty(surface.Types[0].Functions);
    }

    [Fact]
    public void Scan_ReceiverMethods_ExcludesMethodsFromTopLevelFunctions()
    {
        var surface = new GoScanner().Scan(Fixture("methods.go"));
        Assert.Single(surface.Types);
        Assert.Empty(surface.Types[0].Functions);
    }

    [Fact]
    public void Scan_Directory_ExcludesTestFiles()
    {
        var dir = Path.GetDirectoryName(Fixture("public_functions.go"))!;
        var surface = new GoScanner().Scan(dir);

        Assert.Single(surface.Types);
        Assert.Equal(5, surface.Types[0].Functions.Count);
    }

    [Fact]
    public void Scan_NonExistentPath_ReturnsNoTypes()
    {
        var surface = new GoScanner().Scan(Path.Combine(AppContext.BaseDirectory, "does-not-exist"));
        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_SourcePath_IsPreserved()
    {
        var path = Fixture("public_functions.go");
        var surface = new GoScanner().Scan(path);
        Assert.Equal(path, surface.SourcePath);
    }
}
