using System.Text;
using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Go;

public class GoWrapperEmitter : IWrapperEmitter
{
    private const string GoSdkVersion = "v1.7.0";
    private const string GoSdkModule = "github.com/modelcontextprotocol/go-sdk";

    private readonly IFileSystem _fileSystem;

    public GoWrapperEmitter(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public EmittedProject Emit(ScannedSurface surface, BuildContext context)
    {
        var sourceDir = _fileSystem.FileExists(context.SourcePath)
            ? Path.GetDirectoryName(context.SourcePath)!
            : context.SourcePath;

        var (moduleDir, modulePath) = FindSourceModule(sourceDir);
        var importPath = BuildImportPath(modulePath, moduleDir, sourceDir);
        var replacePath = ToPosixPath(Path.GetRelativePath(context.GeneratedProjectPath, moduleDir));

        var files = new List<EmittedFile>
        {
            new("go.mod", GenerateGoMod(context.ServerName, modulePath, replacePath)),
            new("main.go", GenerateMain(surface, context, importPath)),
        };

        return new EmittedProject(context.GeneratedProjectPath, files);
    }

    private (string ModuleDir, string ModulePath) FindSourceModule(string sourceDir)
    {
        var dir = sourceDir;
        while (true)
        {
            var goModPath = Path.Combine(dir, "go.mod");
            if (_fileSystem.FileExists(goModPath))
            {
                var modulePath = ParseModulePath(_fileSystem.ReadAllText(goModPath));
                return (dir, modulePath);
            }

            var parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent) || parent == dir)
                throw new InvalidOperationException($"No go.mod found above source path '{sourceDir}'.");

            dir = parent;
        }
    }

    private static string ParseModulePath(string goModContents)
    {
        foreach (var line in goModContents.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("module ", StringComparison.Ordinal))
                return trimmed["module ".Length..].Trim();
        }

        throw new InvalidOperationException("go.mod does not contain a module directive.");
    }

    private static string BuildImportPath(string modulePath, string moduleDir, string sourceDir)
    {
        var relative = Path.GetRelativePath(moduleDir, sourceDir);
        if (relative == ".") return modulePath;
        return modulePath + "/" + ToPosixPath(relative);
    }

    private static string ToPosixPath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static string GenerateGoMod(string serverName, string sourceModulePath, string replacePath) =>
        $"""
        module x2mcp/generated/{serverName}

        go 1.23

        require (
        	{GoSdkModule} {GoSdkVersion}
        	{sourceModulePath} v0.0.0
        )

        replace {sourceModulePath} => {replacePath}
        """;

    private static string GenerateMain(ScannedSurface surface, BuildContext context, string importPath)
    {
        var structs = new StringBuilder();
        var registrations = new StringBuilder();
        var instances = new StringBuilder();
        var receiverInstanceNames = new Dictionary<(string PackageName, string ReceiverName), string>();

        foreach (var type in surface.Types)
        {
            foreach (var func in type.Functions)
            {
                var isMethod = type.Namespace.Length > 0;
                var ownerName = isMethod ? type.Name : "pkg";
                var argsTypeName = BuildArgsTypeName(ownerName, func.Name);
                var toolName = BuildToolName(ownerName, func.Name, isMethod);

                string call;
                if (isMethod)
                {
                    var key = (type.Namespace, type.Name);
                    if (!receiverInstanceNames.TryGetValue(key, out var instanceName))
                    {
                        instanceName = BuildInstanceName(type.Name, receiverInstanceNames.Count);
                        receiverInstanceNames[key] = instanceName;
                        if (instances.Length > 0) instances.AppendLine();
                        instances.Append($"\t{instanceName} := new(srcpkg.{type.Name})");
                    }

                    call = BuildCallExpression($"{instanceName}.{func.Name}", func);
                }
                else
                {
                    call = BuildCallExpression($"srcpkg.{func.Name}", func);
                }

                if (structs.Length > 0) structs.AppendLine();
                structs.Append(GenerateArgsStruct(argsTypeName, func));

                if (registrations.Length > 0) registrations.AppendLine();
                registrations.Append(GenerateRegistration(func, argsTypeName, toolName, call));
            }
        }

        var runBlock = context.Transport == Transport.Stdio
            ? """
              	if err := server.Run(context.Background(), &mcp.StdioTransport{}); err != nil {
              		log.Printf("server failed: %v", err)
              	}
              """
            : """
              	handler := mcp.NewStreamableHTTPHandler(func(*http.Request) *mcp.Server { return server }, &mcp.StreamableHTTPOptions{Stateless: true})
              	if err := http.ListenAndServe(":8080", handler); err != nil {
              		log.Printf("server failed: %v", err)
              	}
              """;

        var extraImport = context.Transport == Transport.Stdio ? "" : "\n\t\"net/http\"";

        return $$"""
            package main

            import (
            	"context"
            	"log"{{extraImport}}

            	"{{GoSdkModule}}/mcp"
            	srcpkg "{{importPath}}"
            )

            {{structs}}
            func main() {
            	server := mcp.NewServer(&mcp.Implementation{Name: "{{context.ServerName}}", Version: "1.0.0"}, nil)

                {{instances}}
            {{registrations}}
            {{runBlock}}
            }
            """;
    }

    private static string GenerateArgsStruct(string argsTypeName, FunctionDescriptor func)
    {
        var fields = new StringBuilder();
        foreach (var param in func.Parameters)
        {
            var fieldName = char.ToUpperInvariant(param.Name[0]) + param.Name[1..];
            fields.AppendLine($"\t{fieldName} {param.Type} `json:\"{param.Name}\"`");
        }

        return $$"""
            type {{argsTypeName}} struct {
            {{fields}}}
            """;
    }

    private static string GenerateRegistration(
        FunctionDescriptor func,
        string argsTypeName,
        string toolName,
        string call)
    {
        var shape = ParseReturnShape(func.ReturnType);
        var body = shape switch
        {
            ReturnShape.None => $"{call}\n\t\treturn nil, nil, nil",
            ReturnShape.ValueOnly => $"result := {call}\n\t\treturn nil, result, nil",
            ReturnShape.ErrorOnly => $"err := {call}\n\t\tif err != nil {{\n\t\t\treturn nil, nil, err\n\t\t}}\n\t\treturn nil, nil, nil",
            ReturnShape.ValueAndError => $"result, err := {call}\n\t\tif err != nil {{\n\t\t\treturn nil, nil, err\n\t\t}}\n\t\treturn nil, result, nil",
            _ => throw new NotSupportedException($"Unsupported Go return shape '{func.ReturnType}' for function '{func.Name}'."),
        };

        return $$"""
            	mcp.AddTool(server, &mcp.Tool{Name: "{{toolName}}"}, func(ctx context.Context, req *mcp.CallToolRequest, args {{argsTypeName}}) (*mcp.CallToolResult, any, error) {
            		{{body}}
            	})
            """;
    }

    private static string BuildCallExpression(string callableName, FunctionDescriptor func)
    {
        var argList = string.Join(", ", func.Parameters.Select(p =>
            $"args.{char.ToUpperInvariant(p.Name[0])}{p.Name[1..]}"));
        return $"{callableName}({argList})";
    }

    private static string BuildArgsTypeName(string ownerName, string functionName) =>
        $"{SanitizeIdentifier(ownerName)}{functionName}Args";

    private static string BuildToolName(string ownerName, string functionName, bool isMethod) =>
        isMethod ? $"{ownerName}_{functionName}" : functionName;

    private static string BuildInstanceName(string receiverTypeName, int index) =>
        $"receiver{index}_{SanitizeIdentifier(receiverTypeName)}";

    private static string SanitizeIdentifier(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static ReturnShape ParseReturnShape(string rawReturnType)
    {
        var trimmed = rawReturnType.Trim();
        if (trimmed.Length == 0) return ReturnShape.None;

        if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
            trimmed = trimmed[1..^1].Trim();

        var parts = SplitTopLevel(trimmed);

        return parts.Count switch
        {
            1 when parts[0] == "error" => ReturnShape.ErrorOnly,
            1 => ReturnShape.ValueOnly,
            2 when parts[1] == "error" => ReturnShape.ValueAndError,
            _ => ReturnShape.Unsupported,
        };
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
                case '(' or '[':
                    depth++;
                    break;
                case ')' or ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(s[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }
        parts.Add(s[start..].Trim());
        return parts;
    }

    private enum ReturnShape
    {
        None,
        ValueOnly,
        ErrorOnly,
        ValueAndError,
        Unsupported,
    }
}
