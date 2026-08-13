using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;
using System.Text.RegularExpressions;

namespace X2Mcp.Language.Ruby;

public partial class RubyScanner : IScanner
{
    private readonly IFileSystem _fileSystem;

    public RubyScanner(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public ScannedSurface Scan(string sourcePath)
    {
        var files = ResolveRubyFiles(sourcePath);
        var sourceIsDirectory = _fileSystem.DirectoryExists(sourcePath);
        var scanRoot = sourceIsDirectory
            ? sourcePath
            : Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var types = new List<TypeDescriptor>();

        foreach (var file in files)
        {
            var moduleName = BuildModuleName(file, scanRoot, sourceIsDirectory);
            var moduleFunctions = new List<FunctionDescriptor>();
            var classFunctions = new Dictionary<string, List<FunctionDescriptor>>(StringComparer.Ordinal);

            var currentClass = string.Empty;
            var classDepth = 0;
            var methodDepth = 0;
            var currentVisibility = Visibility.Public;

            var lines = _fileSystem.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
            foreach (var rawLine in lines)
            {
                var stripped = StripCommentsAndStrings(rawLine).Trim();
                if (stripped.Length == 0)
                    continue;

                if (stripped == "end")
                {
                    if (methodDepth > 0)
                    {
                        methodDepth--;
                        continue;
                    }

                    if (classDepth > 0)
                    {
                        classDepth--;
                        if (classDepth == 0)
                        {
                            currentClass = string.Empty;
                            currentVisibility = Visibility.Public;
                        }
                    }

                    continue;
                }

                if (classDepth > 0 && methodDepth == 0)
                {
                    if (stripped == "private")
                    {
                        currentVisibility = Visibility.Private;
                        continue;
                    }

                    if (stripped == "protected")
                    {
                        currentVisibility = Visibility.Protected;
                        continue;
                    }

                    if (stripped == "public")
                    {
                        currentVisibility = Visibility.Public;
                        continue;
                    }
                }

                if (ClassRegex().IsMatch(stripped))
                {
                    classDepth++;
                    if (classDepth == 1)
                    {
                        var classMatch = ClassRegex().Match(stripped);
                        currentClass = classMatch.Groups["name"].Value;
                        currentVisibility = Visibility.Public;
                    }

                    continue;
                }

                var methodMatch = MethodRegex().Match(stripped);
                if (!methodMatch.Success)
                    continue;

                methodDepth++;

                if (methodDepth > 1)
                    continue;

                var functionName = methodMatch.Groups["name"].Value;
                if (!IsPublic(functionName))
                    continue;

                var parameters = ParseParameters(methodMatch.Groups["params"].Value);
                var function = new FunctionDescriptor(functionName, parameters, string.Empty, false);

                if (classDepth == 0)
                {
                    moduleFunctions.Add(function);
                    continue;
                }

                if (currentClass.Length == 0 || !IsPublicClass(currentClass) || currentVisibility != Visibility.Public)
                    continue;

                if (!classFunctions.TryGetValue(currentClass, out var methods))
                    classFunctions[currentClass] = methods = [];

                methods.Add(function);
            }

            if (moduleFunctions.Count > 0)
                types.Add(new TypeDescriptor(moduleName, moduleName, moduleFunctions));

            foreach (var classEntry in classFunctions)
                types.Add(new TypeDescriptor(moduleName, classEntry.Key, classEntry.Value));
        }

        return new ScannedSurface(sourcePath, "ruby", types);
    }

    private string[] ResolveRubyFiles(string sourcePath)
    {
        if (_fileSystem.FileExists(sourcePath))
        {
            if (Path.GetExtension(sourcePath).Equals(".rb", StringComparison.OrdinalIgnoreCase)
                && !IsExcludedFile(sourcePath))
                return [sourcePath];

            return [];
        }

        if (_fileSystem.DirectoryExists(sourcePath))
        {
            return _fileSystem
                .GetFiles(sourcePath, "*.rb", SearchOption.AllDirectories)
                .Where(path => !IsExcludedFile(path))
                .ToArray();
        }

        return [];
    }

    private static string BuildModuleName(string filePath, string scanRoot, bool sourceIsDirectory)
    {
        if (!sourceIsDirectory)
            return Path.GetFileNameWithoutExtension(filePath);

        var relativePath = Path.GetRelativePath(scanRoot, filePath);
        var withoutExtension = Path.ChangeExtension(relativePath, null) ?? relativePath;
        return withoutExtension
            .Replace(Path.DirectorySeparatorChar, '.')
            .Replace(Path.AltDirectorySeparatorChar, '.');
    }

    private static bool IsExcludedFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (fileName.StartsWith("test_", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("_test.rb", StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = filePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        return normalized.Contains("/test/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/spec/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublic(string name) =>
        name.Length > 0 && name[0] != '_';

    private static bool IsPublicClass(string name) =>
        name.Length > 0 && char.IsUpper(name[0]);

    private static string StripCommentsAndStrings(string line)
    {
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var escaped = false;
        var chars = new List<char>(line.Length);

        foreach (var ch in line)
        {
            if (escaped)
            {
                escaped = false;
                if (!inSingleQuote && !inDoubleQuote)
                    chars.Add(ch);
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                if (!inSingleQuote && !inDoubleQuote)
                    chars.Add(ch);
                continue;
            }

            if (ch == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (ch == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (ch == '#' && !inSingleQuote && !inDoubleQuote)
                break;

            if (!inSingleQuote && !inDoubleQuote)
                chars.Add(ch);
        }

        return new string([.. chars]);
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

            var hasDefault = raw.Contains('=', StringComparison.Ordinal);
            var namePart = raw;

            var equalsIndex = raw.IndexOf('=');
            if (equalsIndex >= 0)
                namePart = raw[..equalsIndex].Trim();

            if (namePart.StartsWith("**", StringComparison.Ordinal))
                namePart = namePart[2..];
            else if (namePart.StartsWith('*') || namePart.StartsWith('&'))
                namePart = namePart[1..];

            var requiredKeyword = namePart.EndsWith(':') && !hasDefault;
            namePart = namePart.TrimEnd(':').Trim();
            if (namePart.Length == 0)
                continue;

            result.Add(new ParameterDescriptor(namePart, string.Empty, hasDefault || !requiredKeyword && token.Contains(':', StringComparison.Ordinal)));
        }

        return result;
    }

    private static List<string> SplitTopLevel(string text)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch is '(' or '[' or '{')
            {
                depth++;
            }
            else if (ch is ')' or ']' or '}')
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

    [GeneratedRegex("^class\\s+(?<name>[A-Za-z_]\\w*)\\b")]
    private static partial Regex ClassRegex();

    [GeneratedRegex("^def\\s+(?<name>[A-Za-z_]\\w*)\\s*(?:\\((?<params>[^)]*)\\))?")]
    private static partial Regex MethodRegex();

    private enum Visibility
    {
        Public,
        Protected,
        Private,
    }
}
