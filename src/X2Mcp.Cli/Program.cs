using System.CommandLine;
using X2Mcp.Core.Models;
using X2Mcp.Core.Orchestration;
using X2Mcp.Core.Process;
using X2Mcp.Language.DotNet;
using X2Mcp.Language.Go;
using X2Mcp.Language.Python;
using X2Mcp.Language.Ruby;
using X2Mcp.Language.Rust;

var sourceArg = new Argument<string>("source")
{
    Description = "Path to the source code file or directory to wrap as an MCP server",
};

var transportOption = new Option<string>("--transport")
{
    Description = "Transport to use: stdio or http",
    DefaultValueFactory = _ => "stdio",
};

var outDirOption = new Option<string?>("--out-dir")
{
    Description = "Output directory for the built MCP server (defaults to ./dist/<name>-mcp)",
    DefaultValueFactory = _ => null,
};

var nameOption = new Option<string?>("--name")
{
    Description = "Server name (defaults to the source directory or file name)",
    DefaultValueFactory = _ => null,
};

var rootCommand = new RootCommand("x2mcp — wrap any source code as a self-contained MCP server");
rootCommand.Arguments.Add(sourceArg);
rootCommand.Options.Add(transportOption);
rootCommand.Options.Add(outDirOption);
rootCommand.Options.Add(nameOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var source = parseResult.GetValue(sourceArg)!;
    var transportStr = parseResult.GetValue(transportOption)!;
    var output = parseResult.GetValue(outDirOption);
    var name = parseResult.GetValue(nameOption);

    var transport = transportStr.ToLowerInvariant() switch
    {
        "stdio" => Transport.Stdio,
        "http" => Transport.StreamableHttp,
        _ => throw new InvalidOperationException(
                        $"Unknown transport '{transportStr}'. Use 'stdio' or 'http'."),
    };

    var serverName = name
        ?? Path.GetFileNameWithoutExtension(
               source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        ?? "McpServer";

    if (string.IsNullOrWhiteSpace(serverName))
        serverName = "McpServer";

    var outputPath = string.IsNullOrWhiteSpace(output)
        ? Path.Combine("dist", $"{serverName}-mcp")
        : output;

    var modules = new[]
    {
        (X2Mcp.Core.Abstractions.ILanguageModule)new DotNetModule(),
        new PythonModule(),
        new GoModule(),
        new RustModule(),
        new RubyModule(),
    };

    var engine = new OrchestrationEngine(modules, new ProcessRunner());

    Console.WriteLine($"Scanning {source}...");

    var result = await engine.RunAsync(source, outputPath, serverName, transport, Console.WriteLine);

    if (result.Success)
    {
        Console.WriteLine($"Done. MCP server written to: {result.OutputPath}");
        return 0;
    }

    Console.Error.WriteLine($"Build failed: {result.Error}");
    return 1;
});

return await rootCommand.Parse(args).InvokeAsync();
