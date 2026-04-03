using Mcpify.Core.Abstractions;
using Mcpify.Core.Config;

namespace Mcpify.Language.Go;

public class GoModule : ILanguageModule
{
    private static readonly ToolchainConfig _toolchain = ToolchainConfigLoader.LoadFromEmbeddedResource(
        typeof(GoModule).Assembly,
        "Mcpify.Language.Go.toolchain.json");

    public string Language => "go";
    public IReadOnlyList<string> FileExtensions => _toolchain.SourceExtensions;
    public IScanner Scanner { get; } = new GoScanner();
    public IWrapperEmitter Emitter { get; } = new GoWrapperEmitter();
    public ToolchainConfig Toolchain => _toolchain;
}
