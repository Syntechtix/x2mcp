using X2Mcp.Core.Abstractions;
using X2Mcp.Language.Ruby;

namespace X2Mcp.Language.Ruby.Tests;

public class RubyScannerTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Scan_PublicFunctions_ReturnsRubyLanguage()
    {
        var surface = new RubyScanner().Scan(Fixture("public_functions.rb"));

        Assert.Equal("ruby", surface.Language);
    }

    [Fact]
    public void Scan_PublicFunctions_GroupsIntoSingleModuleType()
    {
        var surface = new RubyScanner().Scan(Fixture("public_functions.rb"));

        Assert.Single(surface.Types);
        Assert.Equal("public_functions", surface.Types[0].Name);
        Assert.Equal("public_functions", surface.Types[0].Namespace);
    }

    [Fact]
    public void Scan_PublicFunctions_HasExpectedExportedFunctions()
    {
        var surface = new RubyScanner().Scan(Fixture("public_functions.rb"));

        Assert.Equal(4, surface.Types[0].Functions.Count);
        Assert.Contains(surface.Types[0].Functions, f => f.Name == "add");
        Assert.Contains(surface.Types[0].Functions, f => f.Name == "greet");
        Assert.DoesNotContain(surface.Types[0].Functions, f => f.Name == "_hidden");
    }

    [Fact]
    public void Scan_OptionalParameters_AreMarkedOptional()
    {
        var surface = new RubyScanner().Scan(Fixture("public_functions.rb"));
        var greet = surface.Types[0].Functions.Single(f => f.Name == "greet");

        Assert.Equal(2, greet.Parameters.Count);
        Assert.Equal("name", greet.Parameters[0].Name);
        Assert.False(greet.Parameters[0].IsOptional);
        Assert.Equal("title", greet.Parameters[1].Name);
        Assert.True(greet.Parameters[1].IsOptional);
    }

    [Fact]
    public void Scan_ClassMethods_CapturesOnlyPublicMethods()
    {
        var surface = new RubyScanner().Scan(Fixture("class_methods.rb"));

        Assert.Single(surface.Types);
        Assert.Equal("Calculator", surface.Types[0].Name);
        Assert.Equal(2, surface.Types[0].Functions.Count);
        Assert.Contains(surface.Types[0].Functions, f => f.Name == "add");
        Assert.Contains(surface.Types[0].Functions, f => f.Name == "multiply");
        Assert.DoesNotContain(surface.Types[0].Functions, f => f.Name == "hidden");
        Assert.DoesNotContain(surface.Types[0].Functions, f => f.Name == "limited");
    }

    [Fact]
    public void Scan_NestedMethodDefinitions_IgnoresInnerMethods()
    {
        var surface = new RubyScanner().Scan(Fixture("nested_methods.rb"));

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("outer", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_StringsAndComments_DoNotProduceFalseFunctions()
    {
        var surface = new RubyScanner().Scan(Fixture("strings_and_comments.rb"));

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("active", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_Directory_ExcludesTestAndSpecFiles_AndBuildsModuleNames()
    {
        var dir = Path.GetDirectoryName(Fixture("public_functions.rb"))!;
        var surface = new RubyScanner().Scan(dir);

        var names = surface.Types.Select(t => t.Namespace).ToList();
        Assert.Contains("public_functions", names);
        Assert.Contains("class_methods", names);
        Assert.DoesNotContain("test_sample", names);
        Assert.DoesNotContain("sample_test", names);
    }

    [Fact]
    public void Scan_NonExistentPath_ReturnsNoTypes()
    {
        var surface = new RubyScanner().Scan(Path.Combine(AppContext.BaseDirectory, "does-not-exist"));

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_ExistingNonRubyFile_ReturnsNoTypes()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/readme.txt").Returns(true);

        var surface = new RubyScanner(fs).Scan("/src/readme.txt");

        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_SourcePath_IsPreserved()
    {
        var path = Fixture("public_functions.rb");
        var surface = new RubyScanner().Scan(path);

        Assert.Equal(path, surface.SourcePath);
    }
}
