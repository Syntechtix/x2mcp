using X2Mcp.Core.Abstractions;
using X2Mcp.Language.Rust;

namespace X2Mcp.Language.Rust.Tests;

public class RustScannerTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Scan_PublicFunctions_ReturnsRustLanguage()
    {
        var surface = new RustScanner().Scan(Fixture("public_functions.rs"));
        Assert.Equal("rust", surface.Language);
    }

    [Fact]
    public void Scan_PublicFunctions_GroupsIntoSingleFunctionsType()
    {
        var surface = new RustScanner().Scan(Fixture("public_functions.rs"));
        Assert.Single(surface.Types);
        Assert.Equal("", surface.Types[0].Namespace);
        Assert.Equal("functions", surface.Types[0].Name);
    }

    [Fact]
    public void Scan_PublicFunctions_HasSixFunctions()
    {
        var surface = new RustScanner().Scan(Fixture("public_functions.rs"));
        Assert.Equal(6, surface.Types[0].Functions.Count);
    }

    [Fact]
    public void Scan_AddFunction_HasCorrectParameters()
    {
        var surface = new RustScanner().Scan(Fixture("public_functions.rs"));
        var add = surface.Types[0].Functions.Single(f => f.Name == "add");

        Assert.Equal(2, add.Parameters.Count);
        Assert.Equal("a", add.Parameters[0].Name);
        Assert.Equal("i32", add.Parameters[0].Type);
        Assert.False(add.Parameters[0].IsOptional);
        Assert.Equal("i32", add.ReturnType);
        Assert.False(add.IsAsync);
    }

    [Fact]
    public void Scan_LogMessage_HasNoReturnType()
    {
        var surface = new RustScanner().Scan(Fixture("public_functions.rs"));
        var log = surface.Types[0].Functions.Single(f => f.Name == "log_message");
        Assert.Equal("", log.ReturnType);
    }

    [Fact]
    public void Scan_Validate_HasResultReturnType()
    {
        var surface = new RustScanner().Scan(Fixture("public_functions.rs"));
        var validate = surface.Types[0].Functions.Single(f => f.Name == "validate");
        Assert.Equal("Result<(), String>", validate.ReturnType);
    }

    [Fact]
    public void Scan_Divide_HasResultReturnType()
    {
        var surface = new RustScanner().Scan(Fixture("public_functions.rs"));
        var divide = surface.Types[0].Functions.Single(f => f.Name == "divide");
        Assert.Equal("Result<f64, String>", divide.ReturnType);
    }

    [Fact]
    public void Scan_FormatValue_HasOptionalParameter()
    {
        var surface = new RustScanner().Scan(Fixture("public_functions.rs"));
        var format = surface.Types[0].Functions.Single(f => f.Name == "format_value");
        Assert.True(format.Parameters.Single(p => p.Name == "format").IsOptional);
    }

    [Fact]
    public void Scan_UnexportedFile_ReturnsNoTypes()
    {
        var surface = new RustScanner().Scan(Fixture("unexported.rs"));
        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_Methods_ExcludesHiddenMethodAndNonPublicStruct()
    {
        var surface = new RustScanner().Scan(Fixture("methods.rs"));

        Assert.Single(surface.Types);
        var calculator = surface.Types[0];
        Assert.Equal("Calculator", calculator.Name);
        Assert.Equal(2, calculator.Functions.Count);
        Assert.Contains(calculator.Functions, f => f.Name == "add");
        Assert.Contains(calculator.Functions, f => f.Name == "multiply");
        Assert.DoesNotContain(calculator.Functions, f => f.Name == "hidden");
        Assert.DoesNotContain(calculator.Functions, f => f.Name == "public_but_internal_type");
    }

    [Fact]
    public void Scan_Directory_ExcludesTestsSubdirectory()
    {
        var dir = Path.GetDirectoryName(Fixture("public_functions.rs"))!;
        var surface = new RustScanner().Scan(dir);

        Assert.Equal(2, surface.Types.Count);
        Assert.DoesNotContain(surface.Types, t => t.Functions.Any(f => f.Name == "should_not_be_scanned"));

        var functionsType = surface.Types.Single(t => t.Name == "functions");
        Assert.Equal("public_functions", functionsType.Namespace);
        Assert.Equal(6, functionsType.Functions.Count);

        var calculatorType = surface.Types.Single(t => t.Name == "Calculator");
        Assert.Equal("methods", calculatorType.Namespace);
        Assert.Equal(2, calculatorType.Functions.Count);
    }

    [Fact]
    public void Scan_NonExistentPath_ReturnsNoTypes()
    {
        var surface = new RustScanner().Scan(Path.Combine(AppContext.BaseDirectory, "does-not-exist"));
        Assert.Empty(surface.Types);
    }

    [Fact]
    public void Scan_SourcePath_IsPreserved()
    {
        var path = Fixture("public_functions.rs");
        var surface = new RustScanner().Scan(path);
        Assert.Equal(path, surface.SourcePath);
    }

    [Fact]
    public void Scan_SrcLibRs_MapsToRootModule()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.DirectoryExists("/crate").Returns(true);
        fileSystem.GetFiles("/crate", "*.rs", SearchOption.AllDirectories).Returns(["/crate/src/lib.rs"]);
        fileSystem.ReadAllText("/crate/src/lib.rs").Returns("pub fn add(a: i32, b: i32) -> i32 {\n    a + b\n}\n");

        var surface = new RustScanner(fileSystem).Scan("/crate");

        Assert.Single(surface.Types);
        Assert.Equal("", surface.Types[0].Namespace);
    }

    [Fact]
    public void Scan_NestedModuleFile_BuildsColonSeparatedModulePath()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.DirectoryExists("/crate").Returns(true);
        fileSystem.GetFiles("/crate", "*.rs", SearchOption.AllDirectories).Returns(["/crate/src/foo/bar.rs"]);
        fileSystem.ReadAllText("/crate/src/foo/bar.rs").Returns("pub fn baz() {}\n");

        var surface = new RustScanner(fileSystem).Scan("/crate");

        Assert.Single(surface.Types);
        Assert.Equal("foo::bar", surface.Types[0].Namespace);
    }

    [Fact]
    public void Scan_ScanRootNestedUnderAncestorNamedTests_DoesNotExcludeEverything()
    {
        // Regression: an ancestor directory literally named "tests" (e.g. this repo's own
        // tests/unit/... layout) must not cause every scanned file to be excluded.
        var fileSystem = Substitute.For<IFileSystem>();
        const string crateDir = "/repo/tests/unit/SomeProject/Fixtures";
        fileSystem.DirectoryExists(crateDir).Returns(true);
        fileSystem.GetFiles(crateDir, "*.rs", SearchOption.AllDirectories)
            .Returns([$"{crateDir}/public_functions.rs"]);
        fileSystem.ReadAllText($"{crateDir}/public_functions.rs").Returns("pub fn add(a: i32, b: i32) -> i32 { a + b }\n");

        var surface = new RustScanner(fileSystem).Scan(crateDir);

        Assert.Single(surface.Types);
    }

    [Fact]
    public void Scan_TestsSubdirectoryOfScanRoot_IsExcluded()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.DirectoryExists("/crate").Returns(true);
        fileSystem.GetFiles("/crate", "*.rs", SearchOption.AllDirectories)
            .Returns(["/crate/lib.rs", "/crate/tests/sample_test.rs"]);
        fileSystem.ReadAllText("/crate/lib.rs").Returns("pub fn add(a: i32, b: i32) -> i32 { a + b }\n");
        fileSystem.ReadAllText("/crate/tests/sample_test.rs").Returns("pub fn should_not_be_scanned() -> bool { true }\n");

        var surface = new RustScanner(fileSystem).Scan("/crate");

        Assert.Single(surface.Types);
        Assert.DoesNotContain(surface.Types, t => t.Functions.Any(f => f.Name == "should_not_be_scanned"));
    }

    [Fact]
    public void Scan_SelfParameterVariants_AreExcludedFromParameters()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var sourcePath = "input.rs";
        fileSystem.FileExists(sourcePath).Returns(true);
        fileSystem.ReadAllText(sourcePath).Returns(
            """
            pub struct Widget;

            impl Widget {
                pub fn by_value(self, n: i32) -> i32 { n }
                pub fn by_ref(&self, n: i32) -> i32 { n }
                pub fn by_mut_ref(&mut self, n: i32) -> i32 { n }
            }
            """);

        var surface = new RustScanner(fileSystem).Scan(sourcePath);

        var widget = surface.Types.Single();
        Assert.All(widget.Functions, f => Assert.Single(f.Parameters));
    }

    [Fact]
    public void Scan_AsyncMethod_IsDetectedAsAsync()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var sourcePath = "input.rs";
        fileSystem.FileExists(sourcePath).Returns(true);
        fileSystem.ReadAllText(sourcePath).Returns(
            """
            pub async fn fetch(id: i32) -> String {
                String::new()
            }
            """);

        var surface = new RustScanner(fileSystem).Scan(sourcePath);

        Assert.True(surface.Types.Single().Functions.Single().IsAsync);
    }
}
