using Xunit;
using X2Mcp.Core.Models;
using X2Mcp.Language.DotNet;

namespace X2Mcp.Language.DotNet.Tests;

public class DotNetModuleTests
{
    private readonly DotNetModule _module = new();

    [Fact]
    public void Language_IsCsharp() => Assert.Equal("csharp", _module.Language);

    [Fact]
    public void FileExtensions_ContainsCsExtension() =>
        Assert.Contains(".cs", _module.FileExtensions);

    [Fact]
    public void Toolchain_IsNotNull() => Assert.NotNull(_module.Toolchain);

    [Fact]
    public void Toolchain_RequiredExecutables_ContainsDotnet() =>
        Assert.Contains("dotnet", _module.Toolchain.RequiredExecutables);

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
    public void Scanner_IsNotNull() => Assert.NotNull(_module.Scanner);

    [Fact]
    public void Emitter_IsNotNull() => Assert.NotNull(_module.Emitter);
}
