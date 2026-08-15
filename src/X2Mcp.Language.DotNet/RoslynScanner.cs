using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace X2Mcp.Language.DotNet;

public class RoslynScanner : IScanner
{
    private readonly IFileSystem _fileSystem;

    public RoslynScanner(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public ScannedSurface Scan(string sourcePath)
    {
        var scanRoot = sourcePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(sourcePath)!
            : sourcePath;

        var files = _fileSystem.DirectoryExists(scanRoot)
            ? _fileSystem.GetFiles(scanRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var rel = Path.GetRelativePath(scanRoot, f).Replace(Path.DirectorySeparatorChar, '/');
                    return !rel.StartsWith("bin/") && !rel.StartsWith("obj/")
                        && !rel.Contains("/bin/") && !rel.Contains("/obj/");
                })
                .ToArray()
            : [sourcePath];

        var types = new List<TypeDescriptor>();

        foreach (var file in files)
        {
            var text = _fileSystem.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetCompilationUnitRoot();

            var publicClasses = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));

            foreach (var classDecl in publicClasses)
            {
                var namespaceName = GetNamespace(classDecl);

                var methods = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
                    .Select(m => new FunctionDescriptor(
                        Name: m.Identifier.Text,
                        Parameters: m.ParameterList.Parameters.Select(MapParameter).ToList(),
                        ReturnType: m.ReturnType.ToString(),
                        IsAsync: m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword))
                            || m.ReturnType.ToString().StartsWith("Task", StringComparison.Ordinal)))
                    .ToList();

                if (methods.Count > 0)
                    types.Add(new TypeDescriptor(namespaceName, classDecl.Identifier.Text, methods));
            }
        }

        return new ScannedSurface(sourcePath, "csharp", types);
    }

    // Extracted so the p.Type == null fallback (which the C# parser never actually produces from
    // ParseText, even for malformed source — it always synthesizes an empty-but-non-null TypeSyntax)
    // can still be exercised directly with a hand-built ParameterSyntax in a unit test.
    public static ParameterDescriptor MapParameter(ParameterSyntax p) =>
        new(
            Name: p.Identifier.Text,
            Type: p.Type?.ToString() ?? "object",
            IsOptional: p.Default != null);

    private static string GetNamespace(SyntaxNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is NamespaceDeclarationSyntax ns)
                return ns.Name.ToString();
            if (parent is FileScopedNamespaceDeclarationSyntax fns)
                return fns.Name.ToString();

            parent = parent.Parent;
        }
        return string.Empty;
    }
}
