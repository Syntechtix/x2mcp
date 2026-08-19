using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;
using X2Mcp.Language.Ruby;

namespace X2Mcp.Language.Ruby.Tests;

public class RubyWrapperEmitterTests
{
    private static BuildContext MakeContext(
        string sourcePath,
        Transport transport = Transport.Stdio,
        string serverName = "TestServer") =>
        new(sourcePath, "/out", $"/gen/{serverName}", serverName, transport);

    private static ScannedSurface MakeSurface(params TypeDescriptor[] types) =>
        new("/fake/source", "ruby", types);

    [Fact]
    public void Emit_IncludesServerBuildAndSourceFiles_ForSingleFileInput()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/lib.rb").Returns(true);
        fs.ReadAllText("/src/lib.rb").Returns("def add(a, b)\n  a + b\nend\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var project = new RubyWrapperEmitter(fs).Emit(surface, MakeContext("/src/lib.rb"));

        Assert.Equal(3, project.Files.Count);
        Assert.Contains(project.Files, f => f.RelativePath == "server.rb");
        Assert.Contains(project.Files, f => f.RelativePath == "build.rb");
        Assert.Contains(project.Files, f => f.RelativePath == "lib.rb");
    }

    [Fact]
    public void Emit_DirectoryInput_CopiesRelativeRubyFiles()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.rb", SearchOption.AllDirectories).Returns([
            "/src/lib.rb",
            "/src/pkg/nested.rb",
            "/src/test_helper.rb",
            "/src/spec/model_spec.rb",
        ]);
        fs.ReadAllText("/src/lib.rb").Returns("x = 1\n");
        fs.ReadAllText("/src/pkg/nested.rb").Returns("y = 2\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var project = new RubyWrapperEmitter(fs).Emit(surface, MakeContext("/src"));

        Assert.Contains(project.Files, f => f.RelativePath == "lib.rb");
        Assert.Contains(project.Files, f => f.RelativePath == Path.Combine("pkg", "nested.rb"));
        Assert.DoesNotContain(project.Files, f => f.RelativePath == "test_helper.rb");
        Assert.DoesNotContain(project.Files, f => f.RelativePath == Path.Combine("spec", "model_spec.rb"));
    }

    [Fact]
    public void Emit_ExistingNonRubyFile_ProducesOnlyGeneratedFiles()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/readme.txt").Returns(true);

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var project = new RubyWrapperEmitter(fs).Emit(surface, MakeContext("/src/readme.txt"));

        Assert.Equal(2, project.Files.Count);
        Assert.Contains(project.Files, f => f.RelativePath == "server.rb");
        Assert.Contains(project.Files, f => f.RelativePath == "build.rb");
    }

    [Fact]
    public void Emit_NonExistentSourcePath_ProducesOnlyGeneratedFiles()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/missing").Returns(false);
        fs.DirectoryExists("/src/missing").Returns(false);

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var project = new RubyWrapperEmitter(fs).Emit(surface, MakeContext("/src/missing"));

        Assert.Equal(2, project.Files.Count);
        Assert.Contains(project.Files, f => f.RelativePath == "server.rb");
        Assert.Contains(project.Files, f => f.RelativePath == "build.rb");
    }

    [Fact]
    public void Emit_Server_RequiresDistinctModules()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/lib.rb").Returns(true);
        fs.ReadAllText("/src/lib.rb").Returns("\n");

        var surface = MakeSurface(
            new TypeDescriptor("lib", "lib", [new FunctionDescriptor("add", [], string.Empty, false)]),
            new TypeDescriptor("pkg.nested", "pkg.nested", [new FunctionDescriptor("greet", [], string.Empty, false)]));

        var server = new RubyWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.rb"))
            .Files.Single(f => f.RelativePath == "server.rb").Content;

        Assert.Contains("SERVER_NAME = 'TestServer'", server);
        Assert.Contains("'add' =>", server);
        Assert.Contains("'greet' =>", server);
    }

    [Fact]
    public void Emit_Server_RegistersClassMethodsWithPrefixedToolNames()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/lib.rb").Returns(true);
        fs.ReadAllText("/src/lib.rb").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "Calculator", [
            new FunctionDescriptor("add", [new ParameterDescriptor("a", string.Empty, false)], string.Empty, false),
        ]));

        var server = new RubyWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.rb"))
            .Files.Single(f => f.RelativePath == "server.rb").Content;

        Assert.Contains("'Calculator_add' =>", server);
        Assert.Contains("Object.const_get('Calculator').new", server);
        Assert.Contains("args['a']", server);
    }

    [Fact]
    public void Emit_Server_ParameterSchema_DoesNotClaimStringType()
    {
        // Regression test: Ruby has no static type info, so previously every parameter was
        // declared `type: 'string'` in the advertised JSON Schema. A schema-honoring MCP client
        // would then send string arguments, and Ruby's `+` would silently concatenate instead of
        // add for numeric-looking tools. Omitting the type constraint is the honest schema.
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/lib.rb").Returns(true);
        fs.ReadAllText("/src/lib.rb").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", [
            new FunctionDescriptor("add", [new ParameterDescriptor("a", string.Empty, false)], string.Empty, false),
        ]));

        var server = new RubyWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.rb"))
            .Files.Single(f => f.RelativePath == "server.rb").Content;

        Assert.DoesNotContain("type: 'string'", server);
        Assert.Contains("properties[param[:name]] = {}", server);
    }

    [Fact]
    public void Emit_BuildScript_ContainsLauncherGeneration()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/lib.rb").Returns(true);
        fs.ReadAllText("/src/lib.rb").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var buildScript = new RubyWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.rb"))
            .Files.Single(f => f.RelativePath == "build.rb").Content;

        Assert.Contains("Gem.win_platform?", buildScript);
        Assert.Contains("server_name", buildScript);
        Assert.Contains("bundle_dir", buildScript);
    }

    [Fact]
    public void Emit_DirectoryInput_ExcludesTestSubdirectoryFiles()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src").Returns(false);
        fs.DirectoryExists("/src").Returns(true);
        fs.GetFiles("/src", "*.rb", SearchOption.AllDirectories).Returns([
            "/src/lib.rb",
            "/src/test/helper.rb",
        ]);
        fs.ReadAllText("/src/lib.rb").Returns("x = 1\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));
        var project = new RubyWrapperEmitter(fs).Emit(surface, MakeContext("/src"));

        Assert.Contains(project.Files, f => f.RelativePath == "lib.rb");
        Assert.DoesNotContain(project.Files, f => f.RelativePath == Path.Combine("test", "helper.rb"));
    }

    [Fact]
    public void Emit_Server_OptionalParameter_MarksParametersLiteralOptionalTrue()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/lib.rb").Returns(true);
        fs.ReadAllText("/src/lib.rb").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("lib", "lib", [
            new FunctionDescriptor("greet", [new ParameterDescriptor("name", string.Empty, true)], string.Empty, false),
        ]));

        var server = new RubyWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.rb"))
            .Files.Single(f => f.RelativePath == "server.rb").Content;

        Assert.Contains("optional: true", server);
    }

    [Fact]
    public void Emit_ProjectPath_MatchesGeneratedProjectPath()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/lib.rb").Returns(true);
        fs.ReadAllText("/src/lib.rb").Returns("\n");

        var context = MakeContext("/src/lib.rb");
        var surface = MakeSurface(new TypeDescriptor("lib", "lib", []));

        var project = new RubyWrapperEmitter(fs).Emit(surface, context);

        Assert.Equal(context.GeneratedProjectPath, project.ProjectPath);
    }

    [Fact]
    public void Emit_NamespaceWithSpecialChars_SanitizesInstanceName()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("/src/lib.rb").Returns(true);
        fs.ReadAllText("/src/lib.rb").Returns("\n");

        var surface = MakeSurface(new TypeDescriptor("my-lib", "MyClass", [
            new FunctionDescriptor("run", [], string.Empty, false),
        ]));

        var server = new RubyWrapperEmitter(fs)
            .Emit(surface, MakeContext("/src/lib.rb"))
            .Files.Single(f => f.RelativePath == "server.rb").Content;

        Assert.Contains("instance_my_lib_MyClass", server);
    }
}
