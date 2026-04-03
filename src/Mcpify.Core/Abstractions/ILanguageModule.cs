using Mcpify.Core.Config;
using Mcpify.Core.Models;

namespace Mcpify.Core.Abstractions;

public interface ILanguageModule
{
    string Language { get; }
    IReadOnlyList<string> FileExtensions { get; }
    IScanner Scanner { get; }
    IWrapperEmitter Emitter { get; }
    ToolchainConfig Toolchain { get; }
}
