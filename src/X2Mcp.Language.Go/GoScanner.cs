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
        var packageFunctions = new Dictionary<string, List<FunctionDescriptor>>();
        var methodsByReceiver = new Dictionary<(string Package, string Receiver), List<FunctionDescriptor>>();

        foreach (var file in files)
        {
            var text = _fileSystem.ReadAllText(file);
            var packageMatch = PackageRegex().Match(text);
            if (!packageMatch.Success) continue;

            var packageName = packageMatch.Groups["name"].Value;
            if (!packageFunctions.TryGetValue(packageName, out var functions))
                packageFunctions[packageName] = functions = [];

            foreach (Match funcMatch in FunctionRegex().Matches(text))
            {
                var name = funcMatch.Groups["name"].Value;
                if (!IsExported(name)) continue;

                var parameters = ParseParameters(funcMatch.Groups["params"].Value);
                var returnType = funcMatch.Groups["ret"].Value.Trim();

                functions.Add(new FunctionDescriptor(name, parameters, returnType, false));
            }

            foreach (Match methodMatch in MethodRegex().Matches(text))
            {
                var name = methodMatch.Groups["name"].Value;
                if (!IsExported(name)) continue;

                var receiverType = ExtractReceiverType(methodMatch.Groups["recv"].Value);
                if (receiverType is null || !IsExported(receiverType))
                    continue;

                var key = (packageName, receiverType);
                if (!methodsByReceiver.TryGetValue(key, out var methods))
                    methodsByReceiver[key] = methods = [];

                var parameters = ParseParameters(methodMatch.Groups["params"].Value);
                var returnType = methodMatch.Groups["ret"].Value.Trim();
                methods.Add(new FunctionDescriptor(name, parameters, returnType, false));
            }
        }

        var types = new List<TypeDescriptor>();

        foreach (var kvp in packageFunctions)
        {
            if (kvp.Value.Count > 0)
                types.Add(new TypeDescriptor("", kvp.Key, kvp.Value));
        }

        foreach (var kvp in methodsByReceiver)
        {
            if (kvp.Value.Count > 0)
                types.Add(new TypeDescriptor(kvp.Key.Package, kvp.Key.Receiver, kvp.Value));
        }

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

    private static string? ExtractReceiverType(string receiverRaw)
    {
        var receiver = receiverRaw.Trim();
        if (receiver.Length == 0)
            return null;

        var parts = receiver.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var typeToken = parts[^1].Trim();
        typeToken = typeToken.TrimStart('*');

        var genericStart = typeToken.IndexOf('[');
        if (genericStart >= 0)
            typeToken = typeToken[..genericStart];

        return typeToken.Length == 0 ? null : typeToken;
    }

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

    [GeneratedRegex(@"^func\s*\((?<recv>[^)]*)\)\s*(?<name>[A-Za-z_]\w*)\s*\((?<params>[^)]*)\)\s*(?<ret>[^{]*)\{", RegexOptions.Multiline)]
    private static partial Regex MethodRegex();
}
