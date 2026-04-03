using Mcpify.Core.Abstractions;
using Mcpify.Core.Config;

namespace Mcpify.Language.DotNet;

public class DotNetModule : ILanguageModule
{
    private static readonly ToolchainConfig _toolchain = ToolchainConfigLoader.LoadFromEmbeddedResource(
        typeof(DotNetModule).Assembly,
        "Mcpify.Language.DotNet.toolchain.json");

    public string Language => "csharp";
    public IReadOnlyList<string> FileExtensions => _toolchain.SourceExtensions;
    public IScanner Scanner { get; } = new RoslynScanner();
    public IWrapperEmitter Emitter { get; } = new DotNetWrapperEmitter();
    public ToolchainConfig Toolchain => _toolchain;
}
