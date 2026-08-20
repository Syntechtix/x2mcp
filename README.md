# x2mcp

[![Build](https://github.com/Syntechtix/x2mcp/actions/workflows/build.yml/badge.svg)](https://github.com/Syntechtix/x2mcp/actions/workflows/build.yml)
[![Publish](https://github.com/Syntechtix/x2mcp/actions/workflows/publish.yml/badge.svg)](https://github.com/Syntechtix/x2mcp/actions/workflows/publish.yml)
[![NuGet](https://img.shields.io/nuget/vpre/x2mcp)](https://www.nuget.org/packages/x2mcp)
[![NuGet Downloads](https://img.shields.io/nuget/dt/x2mcp)](https://www.nuget.org/packages/x2mcp)

---

> ### ⚠️ Beta release
> `x2mcp` is in **beta** (`0.1.0-beta`). It's had a manual QA pass across all five
> supported languages— expect rough edges,
> and expect breaking changes before a 1.0 release. Please
> [open an issue](https://github.com/Syntechtix/x2mcp/issues) if something breaks.

---

Create a native-language MCP server wrapper around any language

`x2mcp` scans an existing source file or project, discovers its public
functions/methods, and generates a self-contained, stateless [Model Context
Protocol (MCP)](https://modelcontextprotocol.io/) server that exposes them as MCP
tools — without touching or forking your original code. It emits a small wrapper
project next to your source, wires up the target language's native MCP SDK, and
builds it into a runnable server binary using that language's own toolchain.

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

| Language | Transports | Example |
|---|---|---|
| C# (.NET) | stdio, http | [docs/examples/csharp.md](docs/examples/csharp.md) |
| Go | stdio, http | [docs/examples/go.md](docs/examples/go.md) |
| Python | stdio, http | [docs/examples/python.md](docs/examples/python.md) |
| Ruby | stdio | [docs/examples/ruby.md](docs/examples/ruby.md) |
| Rust | stdio, http | [docs/examples/rust.md](docs/examples/rust.md) |

## Installation

`x2mcp` is published as a [.NET global tool](https://learn.microsoft.com/dotnet/core/tools/global-tools).

Prerequisite: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later.

While `x2mcp` is in beta, no stable version exists yet — `dotnet tool install`/`update`
only consider stable versions by default, so the plain form of these commands will
fail with "package not found" until 1.0 ships. Pass `--prerelease` (or pin an exact
`--version`) until then:

```bash
dotnet tool install --global --prerelease x2mcp
```

Upgrade to the latest version at any time with:

```bash
dotnet tool update --global --prerelease x2mcp
```

## CLI syntax

```
x2mcp <source> [--out-dir <path>] [--transport <stdio|http>] [--name <name>]
```

| Argument / Option | Required | Default | Description |
|---|---|---|---|
| `<source>` | yes | — | Path to the source file or directory to wrap |
| `--out-dir <path>` | no | `./dist/<name>-mcp` | Output directory for the built MCP server |
| `--transport <stdio\|http>` | no | `stdio` | Transport the generated server uses |
| `--name <name>` | no | source file/directory name | Server name |


### Example

```bash
x2mcp ./Calculator --out-dir ./dist/calculator-mcp --name calculator --transport stdio
```

```
Scanning ./Calculator...
Detected language: C#
Creating stdio server...
Done. MCP server written to: ./dist/calculator-mcp
```

