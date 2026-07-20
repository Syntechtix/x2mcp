# x2mcp

Create a native-language MCP server wrapper around any language

`x2mcp` scans an existing source file or project, discovers its public
functions/methods, and generates a self-contained [Model Context Protocol
(MCP)](https://modelcontextprotocol.io/) server that exposes them as MCP tools —
without touching or forking your original code. It emits a small wrapper project
next to your source, wires up the target language's native MCP SDK, and builds it
into a runnable server binary using that language's own toolchain.

## How it works

1. **Detect** — the source path's file extensions are matched against a language
   module (C#, Python, Go, Rust, Ruby).
2. **Scan** — the module's scanner walks the source and produces a surface of
   public types and functions (for C#, this is a Roslyn syntax-tree walk).
3. **Emit** — the module's wrapper emitter generates a companion project: an entry
   point that starts an MCP server plus one tool class per scanned type, each method
   delegating straight to your original code.
4. **Build** — `x2mcp` shells out to the language's own toolchain (e.g.
   `dotnet publish`, `go build`, `cargo build`, `pyinstaller`) to produce a runnable
   server at the requested output path.

## Supported languages

| Language | Status |
|---|---|
| C# (.NET) | ✅ Fully supported — scan, emit, and publish all work today |
| Go | 🚧 Toolchain wired, scanner not yet implemented |
| Python | 🚧 Toolchain wired, scanner not yet implemented |
| Rust | 🚧 Toolchain wired, scanner not yet implemented |
| Ruby | 🚧 Toolchain wired, scanner not yet implemented (stdio transport only) |

See [docs/examples](docs/examples/README.md) for a full walkthrough of every
language, including what's planned for the ones still in progress.

## Installation

`x2mcp` is a .NET project — it's not published as a packaged CLI tool yet, so for
now this is how you run it. (This section will be updated once it ships as an
installable tool.)

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) or later.

```bash
git clone https://github.com/Syntechtix/mcpify.git
cd x2mcp
dotnet build
```

Run it via `dotnet run --project src/X2Mcp.Cli`, or invoke the built `x2mcp`
executable directly from `src/X2Mcp.Cli/bin/Debug/net10.0/`.

## CLI syntax

```
x2mcp <source> [--out <path>] [--transport <stdio|http>] [--name <name>]
```

| Argument / Option | Required | Default | Description |
|---|---|---|---|
| `<source>` | yes | — | Path to the source file or directory to wrap |
| `--out <path>` | no | `./dist/<name>-mcp` | Output directory for the built MCP server |
| `--transport <stdio\|http>` | no | `stdio` | Transport the generated server uses |
| `--name <name>` | no | source file/directory name | Server name |


### Example

```bash
x2mcp ./Calculator --out ./dist/calculator-mcp --name calculator --transport stdio
```

```
Scanning ./Calculator...
Detected language: C#
Creating stdio server...
Done. MCP server written to: ./dist/calculator-mcp
```

## Examples

Full walkthroughs for every supported (and planned) language — including the source
project, the exact command, and the generated wrapper output — live in
[docs/examples](docs/examples/README.md):

- [C#](docs/examples/csharp.md) — fully working today
- [Python](docs/examples/python.md) — planned
- [Go](docs/examples/go.md) — planned
- [Rust](docs/examples/rust.md) — planned
- [Ruby](docs/examples/ruby.md) — planned (stdio transport only)
