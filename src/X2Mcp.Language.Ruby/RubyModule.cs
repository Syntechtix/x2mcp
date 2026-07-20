using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Config;

namespace X2Mcp.Language.Ruby;

public class RubyModule : ILanguageModule
{
    private static readonly ToolchainConfig _toolchain = ToolchainConfigLoader.LoadFromEmbeddedResource(
        typeof(RubyModule).Assembly,
        "X2Mcp.Language.Ruby.toolchain.json");

    public string Language => "ruby";
    public IReadOnlyList<string> FileExtensions => _toolchain.SourceExtensions;
    public IScanner Scanner { get; } = new RubyScanner();
    public IWrapperEmitter Emitter { get; } = new RubyWrapperEmitter();
    public ToolchainConfig Toolchain => _toolchain;
}
