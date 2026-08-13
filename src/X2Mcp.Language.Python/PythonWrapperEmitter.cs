using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;
using System.Text;

namespace X2Mcp.Language.Python;

public class PythonWrapperEmitter : IWrapperEmitter
{
    private readonly IFileSystem _fileSystem;

    public PythonWrapperEmitter(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public EmittedProject Emit(ScannedSurface surface, BuildContext context)
    {
        var files = new List<EmittedFile>();
        var sourceMappings = ResolveSourceMappings(context.SourcePath);

        foreach (var sourceMapping in sourceMappings)
        {
            files.Add(new EmittedFile(sourceMapping.TargetRelativePath, _fileSystem.ReadAllText(sourceMapping.SourcePath)));
        }

        files.Add(new EmittedFile("main.py", GenerateMain(surface, context)));
        return new EmittedProject(context.GeneratedProjectPath, files);
    }

    private List<SourceMapping> ResolveSourceMappings(string sourcePath)
    {
        if (_fileSystem.FileExists(sourcePath))
        {
            if (Path.GetExtension(sourcePath).Equals(".py", StringComparison.OrdinalIgnoreCase)
                && !IsExcludedFile(sourcePath))
            {
                return [new SourceMapping(sourcePath, Path.GetFileName(sourcePath))];
            }

            return [];
        }

        if (!_fileSystem.DirectoryExists(sourcePath))
            return [];

        return _fileSystem
            .GetFiles(sourcePath, "*.py", SearchOption.AllDirectories)
            .Where(path => !IsExcludedFile(path))
            .Select(path => new SourceMapping(path, Path.GetRelativePath(sourcePath, path)))
            .ToList();
    }

    private static string GenerateMain(ScannedSurface surface, BuildContext context)
    {
        var moduleNames = surface.Types.Select(t => t.Namespace)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var lines = new List<string>
        {
            "from mcp.server.fastmcp import FastMCP",
        };

        foreach (var moduleName in moduleNames)
            lines.Add($"import {moduleName}");

        lines.Add(string.Empty);
        lines.Add($"mcp = FastMCP(\"{context.ServerName}\")");

        var classInstanceNames = new Dictionary<(string ModuleName, string ClassName), string>();

        foreach (var type in surface.Types)
        {
            if (type.Namespace == type.Name)
            {
                foreach (var function in type.Functions)
                    lines.Add($"mcp.tool(name=\"{function.Name}\")({type.Namespace}.{function.Name})");

                continue;
            }

            var key = (type.Namespace, type.Name);
            if (!classInstanceNames.TryGetValue(key, out var instanceName))
            {
                instanceName = "_" + SanitizeName($"{type.Namespace}_{type.Name}");
                classInstanceNames[key] = instanceName;
                lines.Add($"{instanceName} = {type.Namespace}.{type.Name}()");
            }

            foreach (var function in type.Functions)
                lines.Add($"mcp.tool(name=\"{function.Name}\")({instanceName}.{function.Name})");
        }

        lines.Add(string.Empty);
        lines.Add("if __name__ == \"__main__\":");
        lines.Add(context.Transport == Transport.Stdio
            ? "    mcp.run(transport=\"stdio\")"
            : "    mcp.run(transport=\"streamable-http\")");

        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            builder.Append(lines[i]);
            if (i < lines.Count - 1)
                builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string SanitizeName(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static bool IsExcludedFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (fileName.StartsWith("test_", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("_test.py", StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = filePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        return normalized.Contains("/__pycache__/", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SourceMapping(string SourcePath, string TargetRelativePath);
}
