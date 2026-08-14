using X2Mcp.Core.Models;
using X2Mcp.Language.Rust;

namespace X2Mcp.Language.Rust.Tests;

public class RustModuleTests
{
    private readonly RustModule _module = new();

    [Fact]
    public void Language_IsRust() => Assert.Equal("rust", _module.Language);

    [Fact]
    public void FileExtensions_ContainsRsExtension() =>
        Assert.Contains(".rs", _module.FileExtensions);

    [Fact]
    public void Toolchain_IsNotNull() => Assert.NotNull(_module.Toolchain);

    [Fact]
    public void Toolchain_RequiredExecutables_ContainsCargo() =>
        Assert.Contains("cargo", _module.Toolchain.RequiredExecutables);

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
    public void Toolchain_PublishCommand_UsesCargoInstall() =>
        Assert.Contains("install", _module.Toolchain.PublishCommand);

    [Fact]
    public void Scanner_IsNotNull() => Assert.NotNull(_module.Scanner);

    [Fact]
    public void Emitter_IsNotNull() => Assert.NotNull(_module.Emitter);
}
