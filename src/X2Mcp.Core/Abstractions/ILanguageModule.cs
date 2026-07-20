using X2Mcp.Core.Config;
using X2Mcp.Core.Models;

namespace X2Mcp.Core.Abstractions;

public interface ILanguageModule
{
    string Language { get; }
    IReadOnlyList<string> FileExtensions { get; }
    IScanner Scanner { get; }
    IWrapperEmitter Emitter { get; }
    ToolchainConfig Toolchain { get; }
}
