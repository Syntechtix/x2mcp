using Mcpify.Core.Abstractions;
using Mcpify.Core.Models;

namespace Mcpify.Core.Orchestration;

public class OrchestrationEngine
{
    private readonly IReadOnlyList<ILanguageModule> _modules;
    private readonly IProcessRunner _processRunner;
    private readonly string _generatedProjectsRoot;

    public OrchestrationEngine(
        IReadOnlyList<ILanguageModule> modules,
        IProcessRunner processRunner,
        string? generatedProjectsRoot = null)
    {
        _modules = modules;
        _processRunner = processRunner;
        _generatedProjectsRoot = generatedProjectsRoot ?? Path.Combine(Path.GetTempPath(), "mcpify");
    }

    public async Task<BuildResult> RunAsync(
        string sourcePath,
        string outputPath,
        string serverName,
        Transport transport,
        CancellationToken ct = default)
    {
        var module = DetectModule(sourcePath);
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
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, file.Content, ct);
        }

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

    private static HashSet<string> GetExtensions(string sourcePath)
    {
        if (File.Exists(sourcePath))
            return [Path.GetExtension(sourcePath)];

        if (Directory.Exists(sourcePath))
        {
            return Directory
                .GetFiles(sourcePath, "*", SearchOption.AllDirectories)
                .Select(Path.GetExtension)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        }

        return [];
    }
}
