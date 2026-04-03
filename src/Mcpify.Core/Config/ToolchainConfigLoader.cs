using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcpify.Core.Config;

public static class ToolchainConfigLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static ToolchainConfig LoadFromEmbeddedResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in assembly '{assembly.GetName().Name}'.");

        return JsonSerializer.Deserialize<ToolchainConfig>(stream, Options)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize toolchain config from resource '{resourceName}'.");
    }
}
