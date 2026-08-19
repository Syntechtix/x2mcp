using System.Text;
using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.DotNet;

public class DotNetWrapperEmitter : IWrapperEmitter
{
    private const string McpSdkVersion = "1.4.1";

    private readonly IFileSystem _fileSystem;

    public DotNetWrapperEmitter(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public EmittedProject Emit(ScannedSurface surface, BuildContext context)
    {
        var sourceCsproj = FindSourceCsproj(context.SourcePath);
        var relativeRef = Path.GetRelativePath(context.GeneratedProjectPath, sourceCsproj)
            .Replace('/', Path.DirectorySeparatorChar);

        var files = new List<EmittedFile>
        {
            new($"{context.ServerName}.csproj", GenerateCsproj(relativeRef, context)),
            new("Program.cs", GenerateProgram(surface, context)),
        };

        foreach (var type in surface.Types)
            files.Add(new EmittedFile($"{type.Name}Tools.cs", GenerateToolClass(type)));

        return new EmittedProject(context.GeneratedProjectPath, files);
    }

    private string FindSourceCsproj(string sourcePath)
    {
        if (sourcePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            && _fileSystem.FileExists(sourcePath))
            return sourcePath;

        var dir = _fileSystem.FileExists(sourcePath)
            ? Path.GetDirectoryName(sourcePath)!
            : sourcePath;

        if (_fileSystem.DirectoryExists(dir))
        {
            var found = _fileSystem
                .GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (found != null) return found;
        }

        var dirName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.Combine(dir, dirName + ".csproj");
    }

    private string GenerateCsproj(string sourceProjectRef, BuildContext context)
    {
        var sdk = context.Transport == Transport.StreamableHttp
            ? "Microsoft.NET.Sdk.Web"
            : "Microsoft.NET.Sdk";
        var assemblyName = $"{context.ServerName}.mcp";

        return $"""
            <Project Sdk="{sdk}">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <AssemblyName>{assemblyName}</AssemblyName>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <ServerExecutableExtension Condition="'$(OS)' == 'Windows_NT'">.exe</ServerExecutableExtension>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="ModelContextProtocol" Version="{McpSdkVersion}" />
                <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.10" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="{sourceProjectRef}" />
              </ItemGroup>
                            <Target Name="CreateServerLauncher" AfterTargets="Publish">
                                <Copy SourceFiles="$(PublishDir)$(AssemblyName)$(ServerExecutableExtension)" DestinationFiles="$(PublishDir){context.ServerName}$(ServerExecutableExtension)" />
                                <Delete Files="$(PublishDir)$(AssemblyName)$(ServerExecutableExtension)" />
                            </Target>
            </Project>
            """;
    }

    private static string GenerateProgram(ScannedSurface surface, BuildContext context)
    {
        var usings = surface.Types
            .Select(t => t.Namespace)
            .Where(ns => !string.IsNullOrEmpty(ns))
            .Distinct()
            .Select(ns => $"using {ns};")
            .ToList();

        var usingBlock = usings.Count > 0 ? string.Join("\n", usings) + "\n" : string.Empty;

        var singletons = surface.Types
            .Select(t => $"services.AddSingleton<{t.Name}>();")
            .ToList();

        var toolRegistrations = surface.Types
            .Select(t => $"    .WithTools<{t.Name}Tools>()")
            .ToList();

        var singletonBlock = string.Join("\n", singletons);
        var toolsBlock = string.Join("\n", toolRegistrations);

        if (context.Transport == Transport.Stdio)
        {
            return $$"""
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.Extensions.Hosting;
                using Microsoft.Extensions.Logging;
                {{usingBlock}}
                var builder = Host.CreateApplicationBuilder(args);
                // The default console logger writes to stdout, which is also the MCP stdio
                // transport's message stream — leaving it enabled corrupts every response with
                // interleaved log text. Clear it so stdout carries only JSON-RPC frames.
                builder.Logging.ClearProviders();
                var services = builder.Services;
                {{singletonBlock}}
                services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                {{toolsBlock}};

                await builder.Build().RunAsync();
                """;
        }
        else
        {
            return $$"""
                {{usingBlock}}
                var builder = WebApplication.CreateBuilder(args);
                var services = builder.Services;
                {{singletonBlock}}
                services
                    .AddMcpServer()
                    .WithHttpTransport(options => options.Stateless = true)
                {{toolsBlock}};

                var app = builder.Build();
                app.MapMcp();
                await app.RunAsync();
                """;
        }
    }

    private static string GenerateToolClass(TypeDescriptor type)
    {
        var instanceName = $"_{char.ToLowerInvariant(type.Name[0])}{type.Name[1..]}";
        var paramName = $"{char.ToLowerInvariant(type.Name[0])}{type.Name[1..]}";
        var usingNs = !string.IsNullOrEmpty(type.Namespace) ? $"using {type.Namespace};\n\n" : string.Empty;

        var methods = new StringBuilder();
        foreach (var func in type.Functions)
        {
            if (methods.Length > 0) methods.AppendLine();
            methods.Append(GenerateToolMethod(instanceName, func));
        }

        return $$"""
            using ModelContextProtocol.Server;
            {{usingNs}}[McpServerToolType]
            public class {{type.Name}}Tools
            {
                private readonly {{type.Name}} {{instanceName}};

                public {{type.Name}}Tools({{type.Name}} {{paramName}})
                    => {{instanceName}} = {{paramName}};

            {{methods}}
            }
            """;
    }

    private static string GenerateToolMethod(string instanceName, FunctionDescriptor func)
    {
        var paramList = string.Join(", ", func.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var argList = string.Join(", ", func.Parameters.Select(p => p.Name));
        var asyncKeyword = func.IsAsync ? "async " : string.Empty;
        var awaitKeyword = func.IsAsync ? "await " : string.Empty;

        return $$"""
                [McpServerTool(Name = "{{func.Name}}")]
                public {{asyncKeyword}}{{func.ReturnType}} {{func.Name}}({{paramList}})
                    => {{awaitKeyword}}{{instanceName}}.{{func.Name}}({{argList}});
            """;
    }
}
