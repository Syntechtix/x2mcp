using X2Mcp.Core.Config;

namespace X2Mcp.Core.Abstractions;

public interface IToolchainValidator
{
    /// <summary>
    /// Checks every executable listed in <see cref="ToolchainConfig.RequiredExecutables"/> and
    /// returns the names of the ones that could not be found/started. An empty list means the
    /// full toolchain is available.
    /// </summary>
    Task<IReadOnlyList<string>> FindMissingExecutablesAsync(
        ToolchainConfig toolchain,
        CancellationToken ct = default);
}
