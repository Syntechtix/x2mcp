using X2Mcp.Core.Config;

namespace X2Mcp.Core.Tests;

public class ToolchainConfigLoaderTests
{
    [Fact]
    public void LoadFromEmbeddedResource_ResourceDoesNotExist_Throws()
    {
        var assembly = typeof(ToolchainConfigLoaderTests).Assembly;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ToolchainConfigLoader.LoadFromEmbeddedResource(assembly, "X2Mcp.Core.Tests.DoesNotExist.json"));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void LoadFromEmbeddedResource_ValidResource_ReturnsDeserializedConfig()
    {
        var assembly = typeof(ToolchainConfigLoaderTests).Assembly;

        var config = ToolchainConfigLoader.LoadFromEmbeddedResource(assembly, "X2Mcp.Core.Tests.ValidConfig.json");

        Assert.Equal("build", config.BuildCommand);
        Assert.Contains("tool", config.RequiredExecutables);
    }

    [Fact]
    public void LoadFromEmbeddedResource_ResourceDeserializesToNull_Throws()
    {
        var assembly = typeof(ToolchainConfigLoaderTests).Assembly;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ToolchainConfigLoader.LoadFromEmbeddedResource(assembly, "X2Mcp.Core.Tests.NullConfig.json"));

        Assert.Contains("Failed to deserialize", ex.Message);
    }
}
