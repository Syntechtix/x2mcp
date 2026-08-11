using X2Mcp.Core.Config;
using X2Mcp.Core.Models;

namespace X2Mcp.Core.Tests;

public class ToolchainConfigTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var config = new ToolchainConfig(
            RequiredExecutables: ["tool"],
            BuildCommand: "build {GeneratedProjectPath}",
            PublishCommand: "publish {GeneratedProjectPath} -o {OutputPath}",
            SupportedTransports: [Transport.Stdio],
            SourceExtensions: [".ext"]);

        Assert.Equal(["tool"], config.RequiredExecutables);
        Assert.Equal("build {GeneratedProjectPath}", config.BuildCommand);
        Assert.Equal("publish {GeneratedProjectPath} -o {OutputPath}", config.PublishCommand);
        Assert.Equal([Transport.Stdio], config.SupportedTransports);
        Assert.Equal([".ext"], config.SourceExtensions);
    }
}
