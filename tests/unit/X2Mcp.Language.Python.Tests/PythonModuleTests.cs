using X2Mcp.Core.Models;
using X2Mcp.Language.Python;

namespace X2Mcp.Language.Python.Tests;

public class PythonModuleTests
{
    private readonly PythonModule _module = new();

    [Fact]
    public void Language_IsPython() => Assert.Equal("python", _module.Language);

    [Fact]
    public void FileExtensions_ContainsPythonExtension() =>
        Assert.Contains(".py", _module.FileExtensions);

    [Fact]
    public void Toolchain_IsNotNull() => Assert.NotNull(_module.Toolchain);

    [Fact]
    public void Toolchain_RequiredExecutables_ContainsPython() =>
        Assert.Contains("python", _module.Toolchain.RequiredExecutables);

    [Fact]
    public void Toolchain_RequiredExecutables_ContainsPyInstaller() =>
        Assert.Contains("pyinstaller", _module.Toolchain.RequiredExecutables);

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
