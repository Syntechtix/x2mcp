using X2Mcp.Core.Models;
using X2Mcp.Language.Ruby;

namespace X2Mcp.Language.Ruby.Tests;

public class RubyModuleTests
{
    private readonly RubyModule _module = new();

    [Fact]
    public void Language_IsRuby() => Assert.Equal("ruby", _module.Language);

    [Fact]
    public void FileExtensions_ContainsRubyExtension() =>
        Assert.Contains(".rb", _module.FileExtensions);

    [Fact]
    public void Toolchain_IsNotNull() => Assert.NotNull(_module.Toolchain);

    [Fact]
    public void Toolchain_RequiredExecutables_ContainsRuby() =>
        Assert.Contains("ruby", _module.Toolchain.RequiredExecutables);

    [Fact]
    public void Toolchain_RequiredExecutables_ContainsBundle() =>
        Assert.Contains("bundle", _module.Toolchain.RequiredExecutables);

    [Fact]
    public void Toolchain_SupportedTransports_ContainsStdio() =>
        Assert.Contains(Transport.Stdio, _module.Toolchain.SupportedTransports);

    [Fact]
    public void Toolchain_PublishCommand_IsNotEmpty() =>
        Assert.NotEmpty(_module.Toolchain.PublishCommand);

    [Fact]
    public void Scanner_IsNotNull() => Assert.NotNull(_module.Scanner);

    [Fact]
    public void Emitter_IsNotNull() => Assert.NotNull(_module.Emitter);
}
