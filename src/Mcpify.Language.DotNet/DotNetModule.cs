using Mcpify.Core.Abstractions;
using Mcpify.Core.Config;
using Mcpify.Core.IO;

namespace Mcpify.Language.DotNet;

public class DotNetModule : ILanguageModule
{
    private static readonly ToolchainConfig _toolchain = ToolchainConfigLoader.LoadFromEmbeddedResource(
        typeof(DotNetModule).Assembly,
        "Mcpify.Language.DotNet.toolchain.json");

    public string Language => "csharp";
    public IReadOnlyList<string> FileExtensions => _toolchain.SourceExtensions;
    public IScanner Scanner { get; }
    public IWrapperEmitter Emitter { get; }
    public ToolchainConfig Toolchain => _toolchain;

    public DotNetModule(IFileSystem? fileSystem = null)
    {
        var fs = fileSystem ?? new FileSystem();
        Scanner = new RoslynScanner(fs);
        Emitter = new DotNetWrapperEmitter(fs);
    }
}
