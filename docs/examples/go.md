# Example: wrapping a Go package

`x2mcp` scans a Go package with a lightweight regex-based parser, finds every
exported (capitalized) top-level function, and generates a companion Go module that
imports your original package and exposes those functions as MCP tools.

## How scanning works

`x2mcp` always produces a single MCP server from whatever source you give it.

- **Directory** (recommended) — recursively scans all `.go` files in the directory
  and every subfolder. This is the normal way to wrap a whole package or module.
- **Single `.go` file** — wraps only the exported functions in that one file.

`_test.go` files are always skipped. Go has no project file concept — the directory
*is* the package.

## Prerequisites

- [Go toolchain](https://go.dev/dl/) — `go` must be on your `PATH` so `x2mcp` can
  shell out to `go build`.
- Your source package must live inside a Go module — a `go.mod` file must exist in
  the source directory or one of its parent directories, since `x2mcp` reads it to
  resolve your package's import path and wires it in via a `replace` directive.

## 1. The source package

A real package typically spans multiple files:

```
calculator/
├── go.mod
├── add.go
└── divide.go
```

```
// go.mod
module github.com/acme/calculator

go 1.23
```

```go
// calculator.go
package calculator

import "fmt"

func Add(a int, b int) int {
    return a + b
}

func Divide(a float64, b float64) (float64, error) {
    if b == 0 {
        return 0, fmt.Errorf("division by zero")
    }
    return a / b, nil
}
```

Only exported, top-level functions are picked up — unexported functions, methods
with receivers, and `_test.go` files are skipped.

## 2. Run x2mcp

Point at the directory (whole package) or a single file:

```bash
# recommended: whole package directory
x2mcp ./calculator --out ./dist/calculator-mcp --name calculator --transport stdio

# or: a single file
x2mcp ./calculator/add.go --out ./dist/calculator-mcp --name calculator --transport stdio
```

- `--out` — where the built, runnable MCP server binary ends up
- `--name` — the server name (also used for the generated project's temp folder)
- `--transport` — `stdio` (default) or `http`

## 3. What gets generated

`x2mcp` writes a temporary wrapper module (under `%TEMP%/x2mcp/<name>` /
`/tmp/x2mcp/<name>`) that looks like this:

```
calculator/
├── go.mod
└── main.go
```

**go.mod** — depends on the MCP Go SDK and your original module, redirected to the
local source path via `replace` so no publishing/tagging is required:

```
module x2mcp/generated/calculator

go 1.23

require (
	github.com/modelcontextprotocol/go-sdk v1.7.0
	github.com/acme/calculator v0.0.0
)

replace github.com/acme/calculator => ../../../calculator
```

**main.go** — one generated `{Func}Args` struct per function plus an
`mcp.AddTool` registration that calls straight into your original package:

```go
package main

import (
	"context"
	"log"

	"github.com/modelcontextprotocol/go-sdk/mcp"
	srcpkg "github.com/acme/calculator"
)

type AddArgs struct {
	A int `json:"a"`
	B int `json:"b"`
}

type DivideArgs struct {
	A float64 `json:"a"`
	B float64 `json:"b"`
}

func main() {
	server := mcp.NewServer(&mcp.Implementation{Name: "calculator", Version: "1.0.0"}, nil)

	mcp.AddTool(server, &mcp.Tool{Name: "Add"}, func(ctx context.Context, req *mcp.CallToolRequest, args AddArgs) (*mcp.CallToolResult, any, error) {
		result := srcpkg.Add(args.A, args.B)
		return nil, result, nil
	})

	mcp.AddTool(server, &mcp.Tool{Name: "Divide"}, func(ctx context.Context, req *mcp.CallToolRequest, args DivideArgs) (*mcp.CallToolResult, any, error) {
		result, err := srcpkg.Divide(args.A, args.B)
		if err != nil {
			return nil, nil, err
		}
		return nil, result, nil
	})

	if err := server.Run(context.Background(), &mcp.StdioTransport{}); err != nil {
		log.Printf("server failed: %v", err)
	}
}
```

Go return signatures map to tool behavior as follows:

| Function returns | Generated behavior                                     |
| ----------------- | ------------------------------------------------------- |
| nothing            | call is fire-and-forget, tool returns no result          |
| `T`                | `T` is returned as the tool result                        |
| `error`            | non-nil error is propagated, otherwise empty result       |
| `(T, error)`       | non-nil error is propagated, otherwise `T` is returned    |

Other shapes (e.g. `(T, U)` or `(T, U, error)`) aren't supported yet and cause
`x2mcp` to fail with a clear error at generation time.

## 4. Build

`x2mcp` then runs the Go toolchain's build command for you:

```bash
go build -mod=mod -o ./dist/calculator-mcp/calculator <generated-project>
```

When it finishes, `./dist/calculator-mcp` contains a runnable MCP server binary you
can point any MCP client at (Claude Desktop, VS Code, the
[MCP Inspector](https://github.com/modelcontextprotocol/inspector), etc.).

## 5. `--transport http`

Passing `--transport http` instead drops the stdio transport and serves MCP over a
stateless streamable HTTP handler on port 8080:

```go
handler := mcp.NewStreamableHTTPHandler(func(*http.Request) *mcp.Server { return server }, &mcp.StreamableHTTPOptions{Stateless: true})
if err := http.ListenAndServe(":8080", handler); err != nil {
	log.Printf("server failed: %v", err)
}
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

The server listens on `:8080` (hardcoded in the generated code). Use
`http://localhost:8080` in the HTTP configs below.

### Claude Desktop

- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
- Linux: `~/.config/Claude/claude_desktop_config.json`
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`

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

Restart Claude after saving.

**HTTP:**

```json
{
  "mcpServers": {
    "calculator": {
      "url": "http://localhost:8080"
    }
  }
}
```

### ChatGPT Codex

- macOS / Linux: `~/.codex/config.toml`
- Windows: `%USERPROFILE%\.codex\config.toml`

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
url = "http://localhost:8080"
```

### VS Code

- macOS / Linux / Windows: `.vscode/mcp.json` (workspace root)

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
      "url": "http://localhost:8080"
    }
  }
}
```

### Visual Studio 2022

- Windows: `.vscode/mcp.json` (solution root, next to your `.sln` / `.slnx`)

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
      "url": "http://localhost:8080"
    }
  }
}
```
