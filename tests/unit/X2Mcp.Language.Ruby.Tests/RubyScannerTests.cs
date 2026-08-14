using X2Mcp.Core.Abstractions;
using X2Mcp.Language.Ruby;

namespace X2Mcp.Language.Ruby.Tests;

public class RubyScannerTests
{
    private const string PublicFunctionsSource = """
        def add(a, b)
          a + b
        end

        def greet(name, title = nil)
          [title, name].compact.join(" ")
        end

        def with_keywords(required:, optional: "x")
          "#{required}-#{optional}"
        end

        def _hidden(value)
          value
        end

        def with_splats(first, *rest, **options, &block)
          [first, rest, options, block]
        end
        """;

    private const string ClassMethodsSource = """
        class Calculator
          def add(a, b)
            a + b
          end

          private

          def hidden(value)
            value
          end

          public

          def multiply(a, b)
            a * b
          end

          protected

          def limited(value)
            value
          end
        end
        """;

    private const string NestedMethodsSource = """
        def outer(value)
          def inner(v)
            v
          end
          value
        end
        """;

    private const string StringsAndCommentsSource = """
        text = "def fake(a)\n  a\nend"
        # def commented_out(a)
        #   a
        # end

        def active(value)
          value
        end
        """;

    private const string BackslashOutsideStringSource = """
        \x

        def visible(value)
          value
        end
        """;

    private const string EdgeCasesSource = """
        text = 'def fake_single(a)\n  a\nend'
        other = "def fake_double(b)\nend"
        escaped = "it\\'s a string"

        def no_params()
          42
        end

        def nested_default(a = [1, 2], b = {x: 1})
          a
        end
        """;

    private static IFileSystem FileAt(string path, string content)
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists(path).Returns(true);
        fs.ReadAllText(path).Returns(content);
        return fs;
    }

    [Fact]
    public void Scan_PublicFunctions_ReturnsRubyLanguage()
    {
        var surface = new RubyScanner(FileAt("/src/public_functions.rb", PublicFunctionsSource))
            .Scan("/src/public_functions.rb");

        Assert.Equal("ruby", surface.Language);
    }

    [Fact]
    public void Scan_PublicFunctions_GroupsIntoSingleModuleType()
    {
        var surface = new RubyScanner(FileAt("/src/public_functions.rb", PublicFunctionsSource))
            .Scan("/src/public_functions.rb");

        Assert.Single(surface.Types);
        Assert.Equal("public_functions", surface.Types[0].Name);
    }

    [Fact]
    public void Scan_PublicFunctions_HasExpectedExportedFunctions()
    {
        var surface = new RubyScanner(FileAt("/src/public_functions.rb", PublicFunctionsSource))
            .Scan("/src/public_functions.rb");

        var names = surface.Types[0].Functions.Select(f => f.Name).ToList();
        Assert.Contains("add", names);
        Assert.Contains("greet", names);
        Assert.Contains("with_keywords", names);
        Assert.Contains("with_splats", names);
        Assert.DoesNotContain("_hidden", names);
    }

    [Fact]
    public void Scan_OptionalParameters_AreMarkedOptional()
    {
        var surface = new RubyScanner(FileAt("/src/public_functions.rb", PublicFunctionsSource))
            .Scan("/src/public_functions.rb");
        var greet = surface.Types[0].Functions.Single(f => f.Name == "greet");

        Assert.Equal(2, greet.Parameters.Count);
        Assert.True(greet.Parameters[1].IsOptional);
    }

    [Fact]
    public void Scan_ClassMethods_CapturesOnlyPublicMethods()
    {
        var surface = new RubyScanner(FileAt("/src/class_methods.rb", ClassMethodsSource))
            .Scan("/src/class_methods.rb");

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
        var surface = new RubyScanner(FileAt("/src/nested_methods.rb", NestedMethodsSource))
            .Scan("/src/nested_methods.rb");

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("outer", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_StringsAndComments_DoNotProduceFalseFunctions()
    {
        var surface = new RubyScanner(FileAt("/src/strings_and_comments.rb", StringsAndCommentsSource))
            .Scan("/src/strings_and_comments.rb");

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("active", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_Directory_ExcludesTestAndSpecFiles_AndBuildsModuleNames()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.rb", SearchOption.AllDirectories).Returns([
            "/src/public_functions.rb",
            "/src/class_methods.rb",
            "/src/test_sample.rb",
            "/src/sample_spec.rb",
        ]);
        fs.ReadAllText("/src/public_functions.rb").Returns(PublicFunctionsSource);
        fs.ReadAllText("/src/class_methods.rb").Returns(ClassMethodsSource);

        var surface = new RubyScanner(fs).Scan("/src");

        var names = surface.Types.Select(t => t.Namespace).ToList();
        Assert.Contains("public_functions", names);
        Assert.Contains("class_methods", names);
        Assert.DoesNotContain("test_sample", names);
        Assert.DoesNotContain("sample_spec", names);
    }

    [Fact]
    public void Scan_NonExistentPath_ReturnsNoTypes()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/does-not-exist").Returns(false);
        fs.DirectoryExists("/does-not-exist").Returns(false);

        var surface = new RubyScanner(fs).Scan("/does-not-exist");

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
    public void Scan_LineWithBackslashOutsideString_DoesNotAffectParsing()
    {
        var surface = new RubyScanner(FileAt("/src/backslash.rb", BackslashOutsideStringSource))
            .Scan("/src/backslash.rb");

        Assert.Single(surface.Types);
        Assert.Single(surface.Types[0].Functions);
        Assert.Equal("visible", surface.Types[0].Functions[0].Name);
    }

    [Fact]
    public void Scan_SourcePath_IsPreserved()
    {
        var surface = new RubyScanner(FileAt("/src/public_functions.rb", PublicFunctionsSource))
            .Scan("/src/public_functions.rb");

        Assert.Equal("/src/public_functions.rb", surface.SourcePath);
    }

    [Fact]
    public void Scan_EdgeCases_HandlesEscapesEmptyParamsAndNestedDefaults()
    {
        var surface = new RubyScanner(FileAt("/src/edge_cases.rb", EdgeCasesSource))
            .Scan("/src/edge_cases.rb");

        Assert.Single(surface.Types);
        var no_params = surface.Types[0].Functions.Single(f => f.Name == "no_params");
        Assert.Empty(no_params.Parameters);
        Assert.Single(surface.Types[0].Functions, f => f.Name == "nested_default");
    }

    [Fact]
    public void Scan_EdgeCases_EmptyParamListProducesNoParameters()
    {
        var surface = new RubyScanner(FileAt("/src/edge_cases.rb", EdgeCasesSource))
            .Scan("/src/edge_cases.rb");

        var no_params = surface.Types[0].Functions.Single(f => f.Name == "no_params");
        Assert.Empty(no_params.Parameters);
    }

    [Fact]
    public void Scan_EdgeCases_NestedDefaultProducesParameters()
    {
        var surface = new RubyScanner(FileAt("/src/edge_cases.rb", EdgeCasesSource))
            .Scan("/src/edge_cases.rb");

        var nested = surface.Types[0].Functions.Single(f => f.Name == "nested_default");
        Assert.Equal(2, nested.Parameters.Count);
        Assert.True(nested.Parameters[0].IsOptional);
        Assert.True(nested.Parameters[1].IsOptional);
    }
}
