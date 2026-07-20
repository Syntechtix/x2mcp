using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Config;

namespace X2Mcp.Language.Rust;

public class RustModule : ILanguageModule
{
    private static readonly ToolchainConfig _toolchain = ToolchainConfigLoader.LoadFromEmbeddedResource(
        typeof(RustModule).Assembly,
        "X2Mcp.Language.Rust.toolchain.json");

    public string Language => "rust";
    public IReadOnlyList<string> FileExtensions => _toolchain.SourceExtensions;
    public IScanner Scanner { get; } = new RustScanner();
    public IWrapperEmitter Emitter { get; } = new RustWrapperEmitter();
    public ToolchainConfig Toolchain => _toolchain;
}
