using Mcpify.Core.Abstractions;
using Mcpify.Core.Config;

namespace Mcpify.Language.Python;

public class PythonModule : ILanguageModule
{
    private static readonly ToolchainConfig _toolchain = ToolchainConfigLoader.LoadFromEmbeddedResource(
        typeof(PythonModule).Assembly,
        "Mcpify.Language.Python.toolchain.json");

    public string Language => "python";
    public IReadOnlyList<string> FileExtensions => _toolchain.SourceExtensions;
    public IScanner Scanner { get; } = new PythonScanner();
    public IWrapperEmitter Emitter { get; } = new PythonWrapperEmitter();
    public ToolchainConfig Toolchain => _toolchain;
}
