using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;
using System.Text.RegularExpressions;

namespace X2Mcp.Language.Python;

public partial class PythonScanner : IScanner
{
    private readonly IFileSystem _fileSystem;

    public PythonScanner(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public ScannedSurface Scan(string sourcePath)
    {
        var files = ResolvePythonFiles(sourcePath);
        var sourceIsDirectory = _fileSystem.DirectoryExists(sourcePath);
        var scanRoot = sourceIsDirectory
            ? sourcePath
            : Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var types = new List<TypeDescriptor>();

        foreach (var file in files)
        {
            var moduleName = BuildModuleName(file, scanRoot, sourceIsDirectory);
            var moduleFunctions = new List<FunctionDescriptor>();
            var classFunctions = new Dictionary<string, List<FunctionDescriptor>>();
            var scopes = new Stack<Scope>();

            var lines = _fileSystem.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
            {
                var trimmedStart = line.TrimStart();
                if (trimmedStart.Length == 0 || trimmedStart.StartsWith('#'))
                    continue;

                var indent = CountIndent(line);
                while (scopes.Count > 0 && indent <= scopes.Peek().Indent)
                    scopes.Pop();

                var classMatch = ClassRegex().Match(trimmedStart);
                if (classMatch.Success)
                {
                    var className = classMatch.Groups["name"].Value;
                    scopes.Push(new Scope(ScopeKind.Class, indent, className, IsPublic(className) && scopes.Count == 0));
                    continue;
                }

                var functionMatch = FunctionRegex().Match(trimmedStart);
                if (!functionMatch.Success)
                    continue;

                var functionName = functionMatch.Groups["name"].Value;
                var isAsync = functionMatch.Groups["async"].Success;
                var parameters = ParseParameters(functionMatch.Groups["params"].Value);
                var returnType = functionMatch.Groups["ret"].Success
                    ? functionMatch.Groups["ret"].Value.Trim()
                    : string.Empty;

                // Checked in this order (rather than IsPublic first) so IsDunder is actually
                // evaluated for every candidate name — IsPublic alone already excludes every
                // dunder (they're always underscore-prefixed), so IsPublic-first would leave
                // IsDunder's true branch permanently unreachable.
                if (!IsDunder(functionName) && IsPublic(functionName) && !IsInsideFunction(scopes))
                {
                    var classScope = GetNearestPublicClassScope(scopes);
                    var descriptor = new FunctionDescriptor(functionName, parameters, returnType, isAsync);

                    if (classScope is null)
                    {
                        moduleFunctions.Add(descriptor);
                    }
                    else
                    {
                        if (!classFunctions.TryGetValue(classScope.Name, out var classMethodList))
                            classFunctions[classScope.Name] = classMethodList = [];

                        classMethodList.Add(descriptor);
                    }
                }

                scopes.Push(new Scope(ScopeKind.Function, indent, functionName, false));
            }

            if (moduleFunctions.Count > 0)
                types.Add(new TypeDescriptor(moduleName, moduleName, moduleFunctions));

            foreach (var classEntry in classFunctions)
                types.Add(new TypeDescriptor(moduleName, classEntry.Key, classEntry.Value));
        }

        return new ScannedSurface(sourcePath, "python", types);
    }

    private string[] ResolvePythonFiles(string sourcePath)
    {
        if (_fileSystem.FileExists(sourcePath))
        {
            if (Path.GetExtension(sourcePath).Equals(".py", StringComparison.OrdinalIgnoreCase)
                && !IsExcludedFile(sourcePath))
                return [sourcePath];

            return [];
        }

        if (_fileSystem.DirectoryExists(sourcePath))
            return _fileSystem
                .GetFiles(sourcePath, "*.py", SearchOption.AllDirectories)
                .Where(path => !IsExcludedFile(path))
                .ToArray();

        return [];
    }

    private static string BuildModuleName(string filePath, string scanRoot, bool sourceIsDirectory)
    {
        if (!sourceIsDirectory)
            return Path.GetFileNameWithoutExtension(filePath);

        var relativePath = Path.GetRelativePath(scanRoot, filePath);
        // ChangeExtension(path, null) only returns null when path itself is null, which
        // Path.GetRelativePath never produces — the null-coalescing fallback is unreachable,
        // so it's dropped in favor of the null-forgiving operator instead of faking a test for it.
        var withoutExtension = Path.ChangeExtension(relativePath, null)!;
        var moduleName = withoutExtension
            .Replace(Path.DirectorySeparatorChar, '.')
            .Replace(Path.AltDirectorySeparatorChar, '.');

        if (moduleName.EndsWith(".__init__", StringComparison.Ordinal))
            moduleName = moduleName[..^".__init__".Length];

        return moduleName.Length == 0
            ? Path.GetFileNameWithoutExtension(filePath)
            : moduleName;
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

    // Public so the empty-string branch (never reachable through the regex-driven Scan() path,
    // since ClassRegex/FunctionRegex both require at least one identifier character) can still
    // be exercised directly by a unit test.
    public static bool IsPublic(string name) =>
        name.Length > 0 && name[0] != '_';

    private static bool IsDunder(string name) =>
        name.Length > 4 && name.StartsWith("__", StringComparison.Ordinal) && name.EndsWith("__", StringComparison.Ordinal);

    private static Scope? GetNearestPublicClassScope(IEnumerable<Scope> scopes)
    {
        foreach (var scope in scopes)
        {
            if (scope.Kind == ScopeKind.Class && scope.IsPublicTopLevel)
                return scope;
        }

        return null;
    }

    private static bool IsInsideFunction(IEnumerable<Scope> scopes)
    {
        foreach (var scope in scopes)
        {
            if (scope.Kind == ScopeKind.Function)
                return true;
        }

        return false;
    }

    private static int CountIndent(string line)
    {
        var count = 0;
        foreach (var ch in line)
        {
            if (ch == ' ')
            {
                count++;
                continue;
            }

            if (ch == '\t')
            {
                count += 4;
                continue;
            }

            break;
        }

        return count;
    }

    private static IReadOnlyList<ParameterDescriptor> ParseParameters(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return [];

        var tokens = SplitTopLevel(parameters);
        var result = new List<ParameterDescriptor>();

        foreach (var token in tokens)
        {
            var raw = token.Trim();
            if (raw.Length == 0)
                continue;

            var equalsIndex = IndexOfTopLevel(raw, '=');
            var hasDefault = equalsIndex >= 0;
            var left = hasDefault ? raw[..equalsIndex].Trim() : raw;

            var colonIndex = IndexOfTopLevel(left, ':');
            var namePart = colonIndex >= 0 ? left[..colonIndex].Trim() : left;
            var typePart = colonIndex >= 0 ? left[(colonIndex + 1)..].Trim() : string.Empty;

            while (namePart.StartsWith('*'))
                namePart = namePart[1..].TrimStart();

            if (namePart.Length == 0
                || namePart.Equals("self", StringComparison.Ordinal)
                || namePart.Equals("cls", StringComparison.Ordinal))
                continue;

            result.Add(new ParameterDescriptor(namePart, typePart, hasDefault));
        }

        return result;
    }

    private static List<string> SplitTopLevel(string text)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (IsOpenBracket(ch))
            {
                depth++;
            }
            else if (IsCloseBracket(ch))
            {
                depth--;
            }
            else if (ch == ',' && depth == 0)
            {
                result.Add(text[start..i]);
                start = i + 1;
            }
        }

        result.Add(text[start..]);
        return result;
    }

    private static int IndexOfTopLevel(string text, char needle)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (IsOpenBracket(ch))
            {
                depth++;
            }
            else if (IsCloseBracket(ch))
            {
                depth--;
            }
            else if (ch == needle && depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsOpenBracket(char ch) => "([{".IndexOf(ch) >= 0;

    private static bool IsCloseBracket(char ch) => ")]}".IndexOf(ch) >= 0;

    [GeneratedRegex("^class\\s+(?<name>[A-Za-z_]\\w*)\\s*(?:\\([^)]*\\))?\\s*:")]
    private static partial Regex ClassRegex();

    [GeneratedRegex("^(?<async>async\\s+)?def\\s+(?<name>[A-Za-z_]\\w*)\\s*\\((?<params>[^)]*)\\)\\s*(?:->\\s*(?<ret>[^:]+))?\\s*:")]
    private static partial Regex FunctionRegex();

    private sealed record Scope(ScopeKind Kind, int Indent, string Name, bool IsPublicTopLevel);

    private enum ScopeKind
    {
        Class,
        Function,
    }
}
