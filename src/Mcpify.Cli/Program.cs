using System.CommandLine;
using Mcpify.Core.Models;
using Mcpify.Core.Orchestration;
using Mcpify.Core.Process;
using Mcpify.Language.DotNet;
using Mcpify.Language.Go;
using Mcpify.Language.Python;
using Mcpify.Language.Ruby;
using Mcpify.Language.Rust;

var sourceArg = new Argument<string>(
    name: "source",
    description: "Path to the source code file or directory to wrap as an MCP server");

var transportOption = new Option<string>(
    name: "--transport",
    getDefaultValue: () => "stdio",
    description: "Transport to use: stdio or http");

var outOption = new Option<string>(
    name: "--out",
    description: "Output directory for the built MCP server");

var nameOption = new Option<string?>(
    name: "--name",
    getDefaultValue: () => null,
    description: "Server name (defaults to the source directory or file name)");

var rootCommand = new RootCommand("mcpify — wrap any source code as a self-contained MCP server");
rootCommand.AddArgument(sourceArg);
rootCommand.AddOption(transportOption);
rootCommand.AddOption(outOption);
rootCommand.AddOption(nameOption);

rootCommand.SetHandler(async (source, transportStr, output, name) =>
{
    var transport = transportStr.ToLowerInvariant() switch
    {
        "stdio" => Transport.Stdio,
        "http"  => Transport.StreamableHttp,
        _       => throw new InvalidOperationException(
                        $"Unknown transport '{transportStr}'. Use 'stdio' or 'http'."),
    };

    var serverName = name
        ?? Path.GetFileNameWithoutExtension(
               source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        ?? "McpServer";

    if (string.IsNullOrWhiteSpace(serverName))
        serverName = "McpServer";

    var modules = new[]
    {
        (Mcpify.Core.Abstractions.ILanguageModule)new DotNetModule(),
        new PythonModule(),
        new GoModule(),
        new RustModule(),
        new RubyModule(),
    };

    var engine = new OrchestrationEngine(modules, new ProcessRunner());

    Console.WriteLine($"Scanning {source}...");

    var result = await engine.RunAsync(source, output, serverName, transport);

    if (result.Success)
    {
        Console.WriteLine($"Done. MCP server written to: {result.OutputPath}");
    }
    else
    {
        Console.Error.WriteLine($"Build failed: {result.Error}");
        Environment.Exit(1);
    }
}, sourceArg, transportOption, outOption, nameOption);

return await rootCommand.InvokeAsync(args);
