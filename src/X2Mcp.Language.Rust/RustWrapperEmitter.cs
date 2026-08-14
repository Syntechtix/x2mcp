using System.Text;
using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;

namespace X2Mcp.Language.Rust;

public class RustWrapperEmitter : IWrapperEmitter
{
    // Must match RustScanner.FreeFunctionGroupName — marks a TypeDescriptor as free functions rather than a struct's methods.
    private const string FreeFunctionGroupName = "functions";
    // The #[tool_router(server_handler)] single-macro pattern used below is the current (post-1.x) API;
    // rmcp 0.x used a different macro shape, so this must track the major version the syntax matches.
    private const string RmcpVersion = "3";

    private readonly IFileSystem _fileSystem;

    public RustWrapperEmitter(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public EmittedProject Emit(ScannedSurface surface, BuildContext context)
    {
        var sourceDir = _fileSystem.FileExists(context.SourcePath)
            ? Path.GetDirectoryName(context.SourcePath)!
            : context.SourcePath;

        var (crateDir, crateName) = FindSourceCrate(sourceDir);
        var relativePath = ToPosixPath(Path.GetRelativePath(context.GeneratedProjectPath, crateDir));
        var binName = SanitizeBinName(context.ServerName);

        var importIdent = crateName.Replace('-', '_');

        var files = new List<EmittedFile>
        {
            new("Cargo.toml", GenerateCargoToml(binName, crateName, relativePath, context.Transport)),
            new("src/main.rs", GenerateMain(surface, context, importIdent)),
        };

        return new EmittedProject(context.GeneratedProjectPath, files);
    }

    private (string CrateDir, string CrateName) FindSourceCrate(string sourceDir)
    {
        var dir = sourceDir;
        while (true)
        {
            var cargoTomlPath = Path.Combine(dir, "Cargo.toml");
            if (_fileSystem.FileExists(cargoTomlPath))
                return (dir, ParseCrateName(_fileSystem.ReadAllText(cargoTomlPath)));

            var parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent) || parent == dir)
                throw new InvalidOperationException($"No Cargo.toml found above source path '{sourceDir}'.");

            dir = parent;
        }
    }

    private static string ParseCrateName(string cargoTomlContents)
    {
        var inPackageSection = false;

        foreach (var rawLine in cargoTomlContents.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith('[')) { inPackageSection = line == "[package]"; continue; }
            if (!inPackageSection) continue;

            var eq = line.IndexOf('=');
            if (eq < 0 || line[..eq].Trim() != "name") continue;

            return line[(eq + 1)..].Trim().Trim('"');
        }

        throw new InvalidOperationException("Cargo.toml does not contain a [package] name.");
    }

    private static string ToPosixPath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static string SanitizeBinName(string serverName)
    {
        var chars = serverName.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        var sanitized = new string(chars);
        return char.IsDigit(sanitized[0]) ? "_" + sanitized : sanitized;
    }

    private static string GenerateCargoToml(string binName, string crateName, string relativePath, Transport transport)
    {
        var httpDeps = transport == Transport.StreamableHttp
            ? "\naxum = \"0.8\"\ntower = \"0.5\""
            : string.Empty;

        return $$"""
            [package]
            name = "{{binName}}"
            version = "0.1.0"
            edition = "2021"

            [[bin]]
            name = "{{binName}}"
            path = "src/main.rs"

            [dependencies]
            rmcp = { version = "{{RmcpVersion}}", features = ["server", "transport-io", "transport-streamable-http-server"] }
            tokio = { version = "1", features = ["macros", "rt-multi-thread"] }
            schemars = "0.8"
            serde = { version = "1", features = ["derive"] }
            anyhow = "1"
            {{crateName}} = { path = "{{relativePath}}" }{{httpDeps}}
            """;
    }

    private static string GenerateMain(ScannedSurface surface, BuildContext context, string importIdent)
    {
        var paramStructs = new StringBuilder();
        var toolMethods = new StringBuilder();

        foreach (var type in surface.Types)
        {
            var isFreeFunctionGroup = type.Name == FreeFunctionGroupName;

            foreach (var func in type.Functions)
            {
                var paramsTypeName = BuildParamsTypeName(type, func, isFreeFunctionGroup);
                var toolName = isFreeFunctionGroup ? func.Name : $"{type.Name}_{func.Name}";
                var call = isFreeFunctionGroup
                    ? BuildCallExpression($"{importIdent}::{func.Name}", func)
                    : BuildCallExpression($"{importIdent}::{type.Name}::default().{func.Name}", func);

                if (paramStructs.Length > 0) paramStructs.AppendLine();
                paramStructs.Append(GenerateParamsStruct(paramsTypeName, func));

                if (toolMethods.Length > 0) toolMethods.AppendLine();
                toolMethods.Append(GenerateToolMethod(paramsTypeName, toolName, call, func));
            }
        }

        var runBlock = context.Transport == Transport.Stdio
            ? """
              let service = GeneratedTools.serve(stdio()).await?;
                  service.waiting().await?;
              """
            : """
              let http_service = StreamableHttpService::new(
                      || Ok(GeneratedTools),
                      Arc::new(LocalSessionManager::default()),
                      StreamableHttpServerConfig::default(),
                  );
                  let router = Router::new().fallback_service(tower::service_fn(move |req| {
                      let http_service = http_service.clone();
                      async move { Ok::<_, std::convert::Infallible>(http_service.handle(req).await) }
                  }));
                  let listener = tokio::net::TcpListener::bind("0.0.0.0:8080").await?;
                  axum::serve(listener, router).await?;
              """;

        var extraUses = context.Transport == Transport.Stdio
            ? "use rmcp::transport::stdio;"
            : """
              use std::sync::Arc;
              use axum::Router;
              use rmcp::transport::streamable_http_server::session::local::LocalSessionManager;
              use rmcp::transport::streamable_http_server::tower::{StreamableHttpServerConfig, StreamableHttpService};
              """;

        return $$"""
            use rmcp::{handler::server::wrapper::Parameters, schemars, tool, tool_router, ServiceExt};
            {{extraUses}}
            use {{importIdent}};

            {{paramStructs}}
            #[derive(Clone)]
            struct GeneratedTools;

            #[tool_router(server_handler)]
            impl GeneratedTools {
            {{toolMethods}}
            }

            #[tokio::main]
            async fn main() -> anyhow::Result<()> {
                {{runBlock}}
                Ok(())
            }
            """;
    }

    private static string GenerateParamsStruct(string paramsTypeName, FunctionDescriptor func)
    {
        var fields = new StringBuilder();
        foreach (var param in func.Parameters)
            fields.AppendLine($"    {param.Name}: {param.Type},");

        return $$"""
            #[derive(Debug, serde::Deserialize, schemars::JsonSchema)]
            struct {{paramsTypeName}} {
            {{fields}}}
            """;
    }

    private static string GenerateToolMethod(string paramsTypeName, string toolName, string call, FunctionDescriptor func)
    {
        var fieldNames = string.Join(", ", func.Parameters.Select(p => p.Name));
        var destructure = $"Parameters({paramsTypeName} {{ {fieldNames} }}): Parameters<{paramsTypeName}>";
        var asyncKeyword = func.IsAsync ? "async " : string.Empty;
        var awaitKeyword = func.IsAsync ? ".await" : string.Empty;

        var body = func.ReturnType.Length == 0
            ? $"{call};\n        \"ok\".to_string()"
            : $"let result = {call}{awaitKeyword};\n        format!(\"{{:?}}\", result)";

        return $$"""
                #[tool(description = "{{toolName}}")]
                {{asyncKeyword}}fn {{toolName}}(&self, {{destructure}}) -> String {
                    {{body}}
                }
            """;
    }

    private static string BuildCallExpression(string callableName, FunctionDescriptor func)
    {
        var argList = string.Join(", ", func.Parameters.Select(p => p.Name));
        return $"{callableName}({argList})";
    }

    private static string BuildParamsTypeName(TypeDescriptor type, FunctionDescriptor func, bool isFreeFunctionGroup)
    {
        var prefix = isFreeFunctionGroup ? string.Empty : type.Name;
        return $"{prefix}{PascalCase(func.Name)}Params";
    }

    private static string PascalCase(string identifier) =>
        string.Concat(identifier.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
