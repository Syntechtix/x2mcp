using X2Mcp.Core.Models;

namespace X2Mcp.Core.Config;

public record ToolchainConfig(
    IReadOnlyList<string> RequiredExecutables,
    string BuildCommand,
    string PublishCommand,
    IReadOnlyList<Transport> SupportedTransports,
    IReadOnlyList<string> SourceExtensions);
