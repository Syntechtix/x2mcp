using System.Text.RegularExpressions;
using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Go;

public partial class GoScanner : IScanner
{
    private readonly IFileSystem _fileSystem;

    public GoScanner(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public ScannedSurface Scan(string sourcePath)
    {
        var files = ResolveGoFiles(sourcePath);
        var typesByPackage = new Dictionary<string, List<FunctionDescriptor>>();

        foreach (var file in files)
        {
            var text = _fileSystem.ReadAllText(file);
            var packageMatch = PackageRegex().Match(text);
            if (!packageMatch.Success) continue;

            var packageName = packageMatch.Groups["name"].Value;
            if (!typesByPackage.TryGetValue(packageName, out var functions))
                typesByPackage[packageName] = functions = [];

            foreach (Match funcMatch in FunctionRegex().Matches(text))
            {
                var name = funcMatch.Groups["name"].Value;
                if (!IsExported(name)) continue;

                var parameters = ParseParameters(funcMatch.Groups["params"].Value);
                var returnType = funcMatch.Groups["ret"].Value.Trim();

                functions.Add(new FunctionDescriptor(name, parameters, returnType, false));
            }
        }

        var types = typesByPackage
            .Select(kvp => new TypeDescriptor("", kvp.Key, kvp.Value))
            .ToList();

        return new ScannedSurface(sourcePath, "go", types);
    }

    private string[] ResolveGoFiles(string sourcePath)
    {
        if (_fileSystem.FileExists(sourcePath))
            return [sourcePath];

        if (_fileSystem.DirectoryExists(sourcePath))
            return _fileSystem
                .GetFiles(sourcePath, "*.go", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                .ToArray();

        return [];
    }

    private static bool IsExported(string name) =>
        name.Length > 0 && char.IsUpper(name[0]);

    private static IReadOnlyList<ParameterDescriptor> ParseParameters(string paramList)
    {
        if (string.IsNullOrWhiteSpace(paramList)) return [];

        var tokens = paramList.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        var result = new List<ParameterDescriptor>();
        var pendingNames = new List<string>();

        foreach (var token in tokens)
        {
            var lastSpace = token.LastIndexOf(' ');
            if (lastSpace < 0)
            {
                pendingNames.Add(token);
                continue;
            }

            var name = token[..lastSpace].Trim();
            var type = token[(lastSpace + 1)..].Trim();

            pendingNames.Add(name);
            foreach (var pendingName in pendingNames)
                result.Add(new ParameterDescriptor(pendingName, type, false));
            pendingNames.Clear();
        }

        return result;
    }

    [GeneratedRegex(@"^package\s+(?<name>\w+)", RegexOptions.Multiline)]
    private static partial Regex PackageRegex();

    [GeneratedRegex(@"^func (?!\()(?<name>[A-Za-z_]\w*)\s*\((?<params>[^)]*)\)\s*(?<ret>[^{]*)\{", RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();
}
