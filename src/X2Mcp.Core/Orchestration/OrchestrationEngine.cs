using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;

namespace X2Mcp.Core.Orchestration;

public class OrchestrationEngine
{
    private readonly IReadOnlyList<ILanguageModule> _modules;
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;
    private readonly string _generatedProjectsRoot;

    public OrchestrationEngine(
        IReadOnlyList<ILanguageModule> modules,
        IProcessRunner processRunner,
        IFileSystem? fileSystem = null,
        string? generatedProjectsRoot = null)
    {
        _modules = modules;
        _processRunner = processRunner;
        _fileSystem = fileSystem ?? new FileSystem();
        _generatedProjectsRoot = generatedProjectsRoot ?? Path.Combine(Path.GetTempPath(), "x2mcp");
    }

    public async Task<BuildResult> RunAsync(
        string sourcePath,
        string outputPath,
        string serverName,
        Transport transport,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        var module = DetectModule(sourcePath);
        progress?.Invoke($"Detected language: {module.Language}");

        var generatedProjectPath = Path.Combine(_generatedProjectsRoot, serverName);

        var context = new BuildContext(
            SourcePath: sourcePath,
            OutputPath: outputPath,
            GeneratedProjectPath: generatedProjectPath,
            ServerName: serverName,
            Transport: transport);

        var surface = module.Scanner.Scan(sourcePath);
        var emittedProject = module.Emitter.Emit(surface, context);

        foreach (var file in emittedProject.Files)
        {
            var fullPath = Path.Combine(generatedProjectPath, file.RelativePath);
            _fileSystem.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await _fileSystem.WriteAllTextAsync(fullPath, file.Content, ct);
        }

        var transportLabel = transport == Transport.Stdio ? "stdio" : "http";
        progress?.Invoke($"Creating {transportLabel} server...");

        var executable = module.Toolchain.RequiredExecutables[0];
        var resolvedArgs = CommandTokenResolver.Resolve(module.Toolchain.PublishCommand, context);
        var result = await _processRunner.RunAsync(executable, resolvedArgs, generatedProjectPath, ct);

        return result.ExitCode == 0
            ? new BuildResult(true, outputPath, null)
            : new BuildResult(false, outputPath, result.StandardError);
    }

    private ILanguageModule DetectModule(string sourcePath)
    {
        var extensions = GetExtensions(sourcePath);

        var module = _modules.FirstOrDefault(m =>
            m.FileExtensions.Any(ext => extensions.Contains(ext, StringComparer.OrdinalIgnoreCase)));

        return module ?? throw new NoLanguageModuleException(sourcePath, extensions);
    }

    private HashSet<string> GetExtensions(string sourcePath)
    {
        if (_fileSystem.FileExists(sourcePath))
            return [Path.GetExtension(sourcePath)];

        if (_fileSystem.DirectoryExists(sourcePath))
        {
            return _fileSystem
                .GetFiles(sourcePath, "*", SearchOption.AllDirectories)
                .Select(Path.GetExtension)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        }

        return [];
    }
}
