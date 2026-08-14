using System.Text.RegularExpressions;
using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Rust;

public partial class RustScanner : IScanner
{
    private const string FreeFunctionGroupName = "functions";

    private readonly IFileSystem _fileSystem;

    public RustScanner(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public ScannedSurface Scan(string sourcePath)
    {
        var files = ResolveRustFiles(sourcePath);
        var sourceIsDirectory = _fileSystem.DirectoryExists(sourcePath);

        var freeFunctionsByModule = new Dictionary<string, List<FunctionDescriptor>>(StringComparer.Ordinal);
        var methodsByModuleAndStruct = new Dictionary<(string Module, string Struct), List<FunctionDescriptor>>();

        foreach (var file in files)
        {
            var text = _fileSystem.ReadAllText(file);
            var modulePath = BuildModulePath(file, sourcePath, sourceIsDirectory);
            var implSpans = FindImplSpans(text);
            var pubStructNames = FindPubStructNames(text);

            foreach (var span in implSpans)
            {
                if (!pubStructNames.Contains(span.StructName)) continue;

                var blockText = text[span.BodyStart..span.BodyEnd];
                var key = (modulePath, span.StructName);
                if (!methodsByModuleAndStruct.TryGetValue(key, out var methods))
                    methodsByModuleAndStruct[key] = methods = [];

                foreach (Match fnMatch in FnRegex().Matches(blockText))
                {
                    var descriptor = BuildFunctionDescriptor(fnMatch);
                    if (descriptor != null) methods.Add(descriptor);
                }
            }

            foreach (Match fnMatch in FnRegex().Matches(text))
            {
                if (IsWithinAnySpan(fnMatch.Index, implSpans)) continue;

                var descriptor = BuildFunctionDescriptor(fnMatch);
                if (descriptor == null) continue;

                if (!freeFunctionsByModule.TryGetValue(modulePath, out var functions))
                    freeFunctionsByModule[modulePath] = functions = [];
                functions.Add(descriptor);
            }
        }

        var types = new List<TypeDescriptor>();

        foreach (var (module, functions) in freeFunctionsByModule)
            if (functions.Count > 0)
                types.Add(new TypeDescriptor(module, FreeFunctionGroupName, functions));

        foreach (var (key, methods) in methodsByModuleAndStruct)
            if (methods.Count > 0)
                types.Add(new TypeDescriptor(key.Module, key.Struct, methods));

        return new ScannedSurface(sourcePath, "rust", types);
    }

    private string[] ResolveRustFiles(string sourcePath)
    {
        if (_fileSystem.FileExists(sourcePath))
            return [sourcePath];

        if (_fileSystem.DirectoryExists(sourcePath))
            return _fileSystem
                .GetFiles(sourcePath, "*.rs", SearchOption.AllDirectories)
                .Where(f => !IsIntegrationTestFile(f, sourcePath))
                .ToArray();

        return [];
    }

    // Checks the path relative to the scan root, not the absolute path — an ancestor directory
    // named "tests" (e.g. this repo's own tests/unit/... layout) must not exclude everything.
    private static bool IsIntegrationTestFile(string path, string scanRoot) =>
        Path.GetRelativePath(scanRoot, path).Replace('\\', '/').Split('/').Contains("tests");

    private static string BuildModulePath(string file, string scanRoot, bool sourceIsDirectory)
    {
        if (!sourceIsDirectory)
            return string.Empty;

        var relative = Path.GetRelativePath(scanRoot, file).Replace('\\', '/');

        const string srcPrefix = "src/";
        if (relative.StartsWith(srcPrefix, StringComparison.Ordinal))
            relative = relative[srcPrefix.Length..];

        if (relative.EndsWith(".rs", StringComparison.Ordinal))
            relative = relative[..^3];

        if (relative.EndsWith("/mod", StringComparison.Ordinal))
            relative = relative[..^4];

        return relative is "lib" or "main" ? string.Empty : relative.Replace("/", "::");
    }

    private static List<ImplSpan> FindImplSpans(string text)
    {
        var spans = new List<ImplSpan>();

        foreach (Match implMatch in ImplRegex().Matches(text))
        {
            var braceStart = text.IndexOf('{', implMatch.Index);
            if (braceStart < 0) continue;

            var braceEnd = FindMatchingBrace(text, braceStart);
            if (braceEnd < 0) continue;

            spans.Add(new ImplSpan(implMatch.Groups["name"].Value, braceStart + 1, braceEnd));
        }

        return spans;
    }

    private static HashSet<string> FindPubStructNames(string text) =>
        PubStructRegex().Matches(text).Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);

    private static bool IsWithinAnySpan(int index, IReadOnlyList<ImplSpan> spans) =>
        spans.Any(s => index >= s.BodyStart && index < s.BodyEnd);

    private static int FindMatchingBrace(string text, int openBraceIndex)
    {
        var depth = 0;
        for (var i = openBraceIndex; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }

    private static FunctionDescriptor? BuildFunctionDescriptor(Match fnMatch)
    {
        var name = fnMatch.Groups["name"].Value;
        var parameters = ParseParameters(fnMatch.Groups["params"].Value);
        var returnType = fnMatch.Groups["ret"].Success ? fnMatch.Groups["ret"].Value.Trim() : string.Empty;
        var isAsync = fnMatch.Groups["async"].Success;

        return new FunctionDescriptor(name, parameters, returnType, isAsync);
    }

    private static IReadOnlyList<ParameterDescriptor> ParseParameters(string paramList)
    {
        var result = new List<ParameterDescriptor>();

        foreach (var token in SplitTopLevel(paramList))
        {
            if (token.Length == 0 || IsSelfParameter(token)) continue;

            var colonIndex = token.IndexOf(':');
            if (colonIndex < 0) continue;

            var name = token[..colonIndex].Trim();
            var type = token[(colonIndex + 1)..].Trim();
            var isOptional = type.StartsWith("Option<", StringComparison.Ordinal);

            result.Add(new ParameterDescriptor(name, type, isOptional));
        }

        return result;
    }

    private static bool IsSelfParameter(string token)
    {
        var trimmed = token.TrimStart('&', ' ').TrimStart();
        if (trimmed.StartsWith("mut ", StringComparison.Ordinal))
            trimmed = trimmed["mut ".Length..].TrimStart();

        return trimmed == "self" || trimmed.StartsWith("self:", StringComparison.Ordinal);
    }

    private static List<string> SplitTopLevel(string s)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < s.Length; i++)
        {
            switch (s[i])
            {
                case '<' or '(' or '[':
                    depth++;
                    break;
                case '>' or ')' or ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(s[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        var last = s[start..].Trim();
        if (last.Length > 0) parts.Add(last);

        return parts;
    }

    private readonly record struct ImplSpan(string StructName, int BodyStart, int BodyEnd);

    [GeneratedRegex(@"^\s*impl\s*(?:<[^>]*>)?\s*(?<name>[A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\{", RegexOptions.Multiline)]
    private static partial Regex ImplRegex();

    [GeneratedRegex(@"^\s*pub(?:\([^)]*\))?\s+struct\s+(?<name>[A-Za-z_]\w*)", RegexOptions.Multiline)]
    private static partial Regex PubStructRegex();

    [GeneratedRegex(@"pub(?:\([^)]*\))?\s+(?<async>async\s+)?fn\s+(?<name>[A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\((?<params>[^)]*)\)\s*(?:->\s*(?<ret>[^\{;]+))?\s*\{")]
    private static partial Regex FnRegex();
}
