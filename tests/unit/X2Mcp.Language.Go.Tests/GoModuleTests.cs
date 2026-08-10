using X2Mcp.Core.Models;
using X2Mcp.Language.Go;

namespace X2Mcp.Language.Go.Tests;

public class GoModuleTests
{
    private readonly GoModule _module = new();

    [Fact]
    public void Language_IsGo() => Assert.Equal("go", _module.Language);

    [Fact]
    public void FileExtensions_ContainsGoExtension() =>
        Assert.Contains(".go", _module.FileExtensions);

    [Fact]
    public void Toolchain_IsNotNull() => Assert.NotNull(_module.Toolchain);

    [Fact]
    public void Toolchain_RequiredExecutables_ContainsGo() =>
        Assert.Contains("go", _module.Toolchain.RequiredExecutables);

    [Fact]
    public void Toolchain_SupportedTransports_ContainsStdio() =>
        Assert.Contains(Transport.Stdio, _module.Toolchain.SupportedTransports);

    [Fact]
    public void Toolchain_SupportedTransports_ContainsStreamableHttp() =>
        Assert.Contains(Transport.StreamableHttp, _module.Toolchain.SupportedTransports);

    [Fact]
    public void Toolchain_PublishCommand_IsNotEmpty() =>
        Assert.NotEmpty(_module.Toolchain.PublishCommand);

    [Fact]
    public void Toolchain_PublishCommand_UsesModMod() =>
        Assert.Contains("-mod=mod", _module.Toolchain.PublishCommand);

    [Fact]
    public void Scanner_IsNotNull() => Assert.NotNull(_module.Scanner);

    [Fact]
    public void Emitter_IsNotNull() => Assert.NotNull(_module.Emitter);
}
