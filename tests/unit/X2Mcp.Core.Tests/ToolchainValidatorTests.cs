using X2Mcp.Core.Config;
using X2Mcp.Core.Models;

namespace X2Mcp.Core.Tests;

public class ToolchainValidatorTests
{
    private static ToolchainConfig MakeToolchain(params string[] requiredExecutables) => new(
        RequiredExecutables: requiredExecutables,
        BuildCommand: "build",
        PublishCommand: "publish",
        SupportedTransports: [Transport.Stdio],
        SourceExtensions: [".stub"]);

    [Fact]
    public async Task FindMissingExecutablesAsync_AllAvailable_ReturnsEmpty()
    {
        var fake = new FakeProcessRunner();
        var validator = new X2Mcp.Core.Toolchain.ToolchainValidator(fake);

        var missing = await validator.FindMissingExecutablesAsync(MakeToolchain("dotnet"));

        Assert.Empty(missing);
    }

    [Fact]
    public async Task FindMissingExecutablesAsync_ToolMissing_ReturnsThatToolName()
    {
        var fake = new FakeProcessRunner { DefaultResult = new ProcessResult(-1, string.Empty, "not found") };
        var validator = new X2Mcp.Core.Toolchain.ToolchainValidator(fake);

        var missing = await validator.FindMissingExecutablesAsync(MakeToolchain("go"));

        Assert.Equal(["go"], missing);
    }

    [Fact]
    public async Task FindMissingExecutablesAsync_SomeToolsMissing_ReturnsOnlyTheMissingOnes()
    {
        var fake = new FakeProcessRunner
        {
            ResultsByExecutable =
            {
                ["ruby"] = new ProcessResult(0, string.Empty, string.Empty),
                ["bundle"] = new ProcessResult(-1, string.Empty, "not found"),
            },
        };
        var validator = new X2Mcp.Core.Toolchain.ToolchainValidator(fake);

        var missing = await validator.FindMissingExecutablesAsync(MakeToolchain("ruby", "bundle"));

        Assert.Equal(["bundle"], missing);
    }

    [Fact]
    public async Task FindMissingExecutablesAsync_NonPythonTool_ProbesWithVersionFlag()
    {
        var fake = new FakeProcessRunner();
        var validator = new X2Mcp.Core.Toolchain.ToolchainValidator(fake);

        await validator.FindMissingExecutablesAsync(MakeToolchain("cargo"));

        Assert.Single(fake.Calls);
        Assert.Equal("cargo", fake.Calls[0].Executable);
        Assert.Equal("--version", fake.Calls[0].Arguments);
    }

    [Fact]
    public async Task FindMissingExecutablesAsync_PyinstallerTool_ProbesThroughPythonModule()
    {
        var fake = new FakeProcessRunner();
        var validator = new X2Mcp.Core.Toolchain.ToolchainValidator(fake);

        await validator.FindMissingExecutablesAsync(MakeToolchain("python", "pyinstaller"));

        Assert.Equal(2, fake.Calls.Count);
        Assert.Equal("python", fake.Calls[1].Executable);
        Assert.Equal("-m PyInstaller --version", fake.Calls[1].Arguments);
    }
}
