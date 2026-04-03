using Mcpify.Core.Abstractions;
using Mcpify.Core.Config;
using Mcpify.Core.Models;

namespace Mcpify.Core.Tests;

public sealed class StubLanguageModule : ILanguageModule
{
    public StubLanguageModule(
        string fileExtension,
        ScannedSurface? surface = null,
        EmittedProject? emittedProject = null)
    {
        FileExtensions = [fileExtension];
        Toolchain = new ToolchainConfig(
            RequiredExecutables: ["stub-tool"],
            BuildCommand: "build {GeneratedProjectPath}",
            PublishCommand: "publish {GeneratedProjectPath} -o {OutputPath}",
            SupportedTransports: [Transport.Stdio, Transport.StreamableHttp],
            SourceExtensions: [fileExtension]);
        Scanner = new ConfigurableScanner(surface);
        Emitter = new ConfigurableEmitter(emittedProject);
    }

    public string Language => "stub";
    public IReadOnlyList<string> FileExtensions { get; }
    public IScanner Scanner { get; }
    public IWrapperEmitter Emitter { get; }
    public ToolchainConfig Toolchain { get; }

    private sealed class ConfigurableScanner : IScanner
    {
        private readonly ScannedSurface? _surface;
        public ConfigurableScanner(ScannedSurface? surface) => _surface = surface;
        public ScannedSurface Scan(string sourcePath) =>
            _surface ?? new ScannedSurface(sourcePath, "stub", []);
    }

    private sealed class ConfigurableEmitter : IWrapperEmitter
    {
        private readonly EmittedProject? _project;
        public ConfigurableEmitter(EmittedProject? project) => _project = project;
        public EmittedProject Emit(ScannedSurface surface, BuildContext context) =>
            _project ?? new EmittedProject(context.GeneratedProjectPath, [
                new EmittedFile("stub.txt", "stub content"),
            ]);
    }
}
