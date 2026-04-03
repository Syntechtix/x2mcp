using Mcpify.Core.Models;

namespace Mcpify.Core.Config;

public record ToolchainConfig(
    IReadOnlyList<string> RequiredExecutables,
    string BuildCommand,
    string PublishCommand,
    IReadOnlyList<Transport> SupportedTransports,
    IReadOnlyList<string> SourceExtensions);
