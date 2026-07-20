using X2Mcp.Language.DotNet;

namespace X2Mcp.Language.DotNet.Tests;

public class RoslynScannerTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, name);

    [Fact]
    public void Scan_PublicClassWithPublicMethods_ReturnsTypes()
    {
        var surface = new RoslynScanner().Scan(Fixture("PublicMethods.cs"));

        Assert.Equal("csharp", surface.Language);
        Assert.Equal(2, surface.Types.Count);
    }

    [Fact]
    public void Scan_Calculator_HasCorrectMethodCount()
    {
        var surface = new RoslynScanner().Scan(Fixture("PublicMethods.cs"));
        var calculator = surface.Types.Single(t => t.Name == "Calculator");
        Assert.Equal(4, calculator.Functions.Count);
    }

    [Fact]
    public void Scan_Calculator_HasCorrectNamespace()
    {
        var surface = new RoslynScanner().Scan(Fixture("PublicMethods.cs"));
        var calculator = surface.Types.Single(t => t.Name == "Calculator");
        Assert.Equal("Fixtures", calculator.Namespace);
    }

    [Fact]
    public void Scan_AddMethod_HasCorrectParameters()
    {
        var surface = new RoslynScanner().Scan(Fixture("PublicMethods.cs"));
        var add = surface.Types.Single(t => t.Name == "Calculator")
                         .Functions.Single(f => f.Name == "Add");

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
        var surface = new RoslynScanner().Scan(Fixture("PublicMethods.cs"));
        var divide = surface.Types.Single(t => t.Name == "Calculator")
                            .Functions.Single(f => f.Name == "DivideAsync");
        Assert.True(divide.IsAsync);
    }

    [Fact]
    public void Scan_FormatMethod_HasOptionalParameter()
    {
        var surface = new RoslynScanner().Scan(Fixture("PublicMethods.cs"));
        var format = surface.Types.Single(t => t.Name == "Calculator")
                            .Functions.Single(f => f.Name == "Format");
        Assert.True(format.Parameters.Single(p => p.Name == "format").IsOptional);
    }

    [Fact]
    public void Scan_InternalClass_ReturnsNoTypes()
    {
        var surface = new RoslynScanner().Scan(Fixture("InternalClass.cs"));
        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_PrivateMethods_ExcludesNonPublic()
    {
        var surface = new RoslynScanner().Scan(Fixture("PrivateMethods.cs"));
        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("PublicMethod", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_Directory_ScansAllCsFiles()
    {
        var dir = Path.GetDirectoryName(Fixture("PublicMethods.cs"))!;
        var surface = new RoslynScanner().Scan(dir);
        // PublicMethods.cs has Calculator + Greeter; PrivateMethods.cs has MixedAccess
        Assert.True(surface.Types.Count >= 3);
    }

    [Fact]
    public void Scan_SourcePath_IsPreserved()
    {
        var path = Fixture("PublicMethods.cs");
        var surface = new RoslynScanner().Scan(path);
        Assert.Equal(path, surface.SourcePath);
    }
}
