# Example: wrapping a C# library

`x2mcp` scans a project with [Roslyn](https://github.com/dotnet/roslyn), finds every
`public` method on every `public` class, and generates a companion MCP server project
that references your original code and exposes those methods as MCP tools.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — `dotnet` must be on your
  `PATH`. The generated `McpServer.csproj` hardcodes
  `<TargetFramework>net10.0</TargetFramework>`, so .NET 10 specifically is required;
  earlier SDKs (8.0, 9.0) can't build the wrapper.

## 1. The source project

```
Calculator/
├── Calculator.csproj
└── Calculator.cs
```

```csharp
// Calculator.cs
namespace Acme.Math;

public class Calculator
{
    public int Add(int a, int b) => a + b;

    public int Subtract(int a, int b) => a - b;

    public async Task<double> DivideAsync(double a, double b)
    {
        await Task.Yield();
        return a / b;
    }
}
```

## 2. Run x2mcp

```bash
x2mcp ./Calculator --out ./dist/calculator-mcp --name calculator --transport stdio
```

- `./Calculator` — the source directory (an entire folder or a single `.cs` file both work)
- `--out` — where the built, runnable MCP server ends up
- `--name` — the server name (also used for the generated project's temp folder)
- `--transport` — `stdio` (default) or `http`

## 3. What gets generated

`x2mcp` writes a temporary wrapper project (under `%TEMP%/x2mcp/<name>` /
`/tmp/x2mcp/<name>`) that looks like this:

```
calculator/
├── McpServer.csproj
├── Program.cs
└── CalculatorTools.cs
```

**McpServer.csproj** — references your original `Calculator.csproj` and the
`ModelContextProtocol` SDK:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="1.4.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.10" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Calculator/Calculator.csproj" />
  </ItemGroup>
</Project>
```

**CalculatorTools.cs** — one `[McpServerTool]` method per public method, delegating
straight to your original class:

```csharp
using ModelContextProtocol.Server;
using Acme.Math;

[McpServerToolType]
public class CalculatorTools
{
    private readonly Calculator _calculator;

    public CalculatorTools(Calculator calculator)
        => _calculator = calculator;

    [McpServerTool(Name = "Add")]
    public int Add(int a, int b)
        => _calculator.Add(a, b);

    [McpServerTool(Name = "Subtract")]
    public int Subtract(int a, int b)
        => _calculator.Subtract(a, b);

    [McpServerTool(Name = "DivideAsync")]
    public async Task<double> DivideAsync(double a, double b)
        => await _calculator.DivideAsync(a, b);
}
```

**Program.cs** — wires up the MCP host and registers the tool class(es):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Acme.Math;

var builder = Host.CreateApplicationBuilder(args);
var services = builder.Services;
services.AddSingleton<Calculator>();
services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<CalculatorTools>();

await builder.Build().RunAsync();
```

## 4. Build and publish

`x2mcp` then runs the .NET toolchain's publish command for you:

```bash
dotnet publish <generated-project> -c Release -o ./dist/calculator-mcp --self-contained
```

When it finishes, `./dist/calculator-mcp` contains a self-contained, runnable MCP
server binary you can point any MCP client at (Claude Desktop, VS Code, the
[MCP Inspector](https://github.com/modelcontextprotocol/inspector), etc.).

## 5. `--transport http`

Passing `--transport http` instead switches the generated project to
`Microsoft.NET.Sdk.Web`, drops the stdio transport, and maps the MCP endpoints over
ASP.NET Core:

```csharp
var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
services.AddSingleton<Calculator>();
services
    .AddMcpServer()
    .WithTools<CalculatorTools>();

var app = builder.Build();
app.MapMcp();
await app.RunAsync();
```

## 6. Connect to an MCP client

Once `./dist/calculator-mcp/` exists, register the server in any MCP client.

> **Absolute paths required.** MCP clients launch the binary as a subprocess; relative
> paths won't resolve. Replace the placeholder paths below with the real absolute path
> on your machine.

### Starting the HTTP server

If you built with `--transport http`, start the binary before configuring any client:

```bash
./dist/calculator-mcp/calculator
```

ASP.NET Core listens on `http://localhost:5000` by default. To use a different port:

```bash
ASPNETCORE_URLS=http://localhost:9000 ./dist/calculator-mcp/calculator
```

```powershell
$env:ASPNETCORE_URLS="http://localhost:9000"; .\dist\calculator-mcp\calculator.exe
```

Use `http://localhost:5000/mcp` (or your custom port) in the HTTP configs below.

### Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or
`%APPDATA%\Claude\claude_desktop_config.json` (Windows):

**stdio:**

```json
{
  "mcpServers": {
    "calculator": {
      "command": "/absolute/path/to/dist/calculator-mcp/calculator",
      "args": []
    }
  }
}
```

On Windows the binary is `calculator.exe`. Restart Claude after saving.

**HTTP:**

```json
{
  "mcpServers": {
    "calculator": {
      "url": "http://localhost:5000/mcp"
    }
  }
}
```

### ChatGPT Codex

Edit `~/.codex/config.toml`:

**stdio:**

```toml
[[mcp_servers]]
name = "calculator"
cmd = ["/absolute/path/to/dist/calculator-mcp/calculator"]
```

**HTTP:**

```toml
[[mcp_servers]]
name = "calculator"
url = "http://localhost:5000/mcp"
```

### VS Code

Create or edit `.vscode/mcp.json` in your workspace root:

**stdio:**

```json
{
  "servers": {
    "calculator": {
      "type": "stdio",
      "command": "${workspaceFolder}/dist/calculator-mcp/calculator"
    }
  }
}
```

The file is picked up automatically — no restart needed. Switch to **Agent mode** and
the calculator tools are available immediately.

**HTTP:**

```json
{
  "servers": {
    "calculator": {
      "type": "http",
      "url": "http://localhost:5000/mcp"
    }
  }
}
```

### Visual Studio 2022

Visual Studio 2022 17.14+ reads the same `.vscode/mcp.json` format. Create the file
at the solution root (next to your `.sln` / `.slnx`):

**stdio:**

```json
{
  "servers": {
    "calculator": {
      "type": "stdio",
      "command": "C:/absolute/path/to/dist/calculator-mcp/calculator.exe"
    }
  }
}
```

Restart Visual Studio after creating or modifying the file. Tools appear in Agent mode.

**HTTP:**

```json
{
  "servers": {
    "calculator": {
      "type": "http",
      "url": "http://localhost:5000/mcp"
    }
  }
}
```
