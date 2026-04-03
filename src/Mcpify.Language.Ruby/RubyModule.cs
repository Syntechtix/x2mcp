using Mcpify.Core.Abstractions;
using Mcpify.Core.Config;

namespace Mcpify.Language.Ruby;

public class RubyModule : ILanguageModule
{
    private static readonly ToolchainConfig _toolchain = ToolchainConfigLoader.LoadFromEmbeddedResource(
        typeof(RubyModule).Assembly,
        "Mcpify.Language.Ruby.toolchain.json");

    public string Language => "ruby";
    public IReadOnlyList<string> FileExtensions => _toolchain.SourceExtensions;
    public IScanner Scanner { get; } = new RubyScanner();
    public IWrapperEmitter Emitter { get; } = new RubyWrapperEmitter();
    public ToolchainConfig Toolchain => _toolchain;
}
