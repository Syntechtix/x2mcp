using X2Mcp.Core.Models;
using X2Mcp.Core.Orchestration;

namespace X2Mcp.Core.Tests;

public class CommandTokenResolverTests
{
    private static BuildContext Ctx(
        string src = "/src", string out_ = "/out", string gen = "/gen",
        string name = "Srv", Transport t = Transport.Stdio) =>
        new(src, out_, gen, name, t);

    [Fact]
    public void Resolve_GeneratedProjectPathToken_IsReplaced()
    {
        var resolved = CommandTokenResolver.Resolve("publish {GeneratedProjectPath} -c Release", Ctx(gen: "/gen/mysvr"));
        Assert.Equal("publish /gen/mysvr -c Release", resolved);
    }

    [Fact]
    public void Resolve_OutputPathToken_IsReplaced()
    {
        var resolved = CommandTokenResolver.Resolve("build -o {OutputPath}", Ctx(out_: "/dist/mysvr"));
        Assert.Equal("build -o /dist/mysvr", resolved);
    }

    [Fact]
    public void Resolve_SourcePathToken_IsReplaced()
    {
        var resolved = CommandTokenResolver.Resolve("run {SourcePath}", Ctx(src: "/my/src"));
        Assert.Equal("run /my/src", resolved);
    }

    [Fact]
    public void Resolve_ServerNameToken_IsReplaced()
    {
        var resolved = CommandTokenResolver.Resolve("build {ServerName}", Ctx(name: "MyApp"));
        Assert.Equal("build MyApp", resolved);
    }

    [Fact]
    public void Resolve_TransportToken_IsReplaced()
    {
        var resolved = CommandTokenResolver.Resolve("run --transport {Transport}", Ctx(t: Transport.StreamableHttp));
        Assert.Equal("run --transport StreamableHttp", resolved);
    }

    [Fact]
    public void Resolve_AllTokens_AreReplaced()
    {
        var ctx = Ctx("/src", "/out", "/gen", "MySvr", Transport.StreamableHttp);
        var resolved = CommandTokenResolver.Resolve(
            "{SourcePath} {OutputPath} {GeneratedProjectPath} {ServerName} {Transport}", ctx);
        Assert.Equal("/src /out /gen MySvr StreamableHttp", resolved);
    }

    [Fact]
    public void Resolve_NoTokens_ReturnsTemplateUnchanged()
    {
        const string template = "build --no-restore";
        Assert.Equal(template, CommandTokenResolver.Resolve(template, Ctx()));
    }

    [Fact]
    public void Resolve_UnknownToken_IsLeftUnchanged()
    {
        const string template = "build {UnknownToken}";
        Assert.Equal(template, CommandTokenResolver.Resolve(template, Ctx()));
    }

    [Fact]
    public void Resolve_ExeSuffixToken_OnWindows_ResolvesToDotExe()
    {
        var resolved = CommandTokenResolver.Resolve("{ServerName}{ExeSuffix}", Ctx(name: "Srv"), isWindows: true);
        Assert.Equal("Srv.exe", resolved);
    }

    [Fact]
    public void Resolve_ExeSuffixToken_OnNonWindows_ResolvesToEmptyString()
    {
        var resolved = CommandTokenResolver.Resolve("{ServerName}{ExeSuffix}", Ctx(name: "Srv"), isWindows: false);
        Assert.Equal("Srv", resolved);
    }
}
