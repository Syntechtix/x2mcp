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
        var files = _fileSystem.DirectoryExists(sourcePath)
            ? _fileSystem.GetFiles(sourcePath, "*.cs", SearchOption.AllDirectories)
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
                        Parameters: m.ParameterList.Parameters
                            .Select(p => new ParameterDescriptor(
                                Name: p.Identifier.Text,
                                Type: p.Type?.ToString() ?? "object",
                                IsOptional: p.Default != null))
                            .ToList(),
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
