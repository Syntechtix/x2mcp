using Mcpify.Core.Abstractions;
using Mcpify.Core.Config;

namespace Mcpify.Language.Rust;

public class RustModule : ILanguageModule
{
    private static readonly ToolchainConfig _toolchain = ToolchainConfigLoader.LoadFromEmbeddedResource(
        typeof(RustModule).Assembly,
        "Mcpify.Language.Rust.toolchain.json");

    public string Language => "rust";
    public IReadOnlyList<string> FileExtensions => _toolchain.SourceExtensions;
    public IScanner Scanner { get; } = new RustScanner();
    public IWrapperEmitter Emitter { get; } = new RustWrapperEmitter();
    public ToolchainConfig Toolchain => _toolchain;
}
