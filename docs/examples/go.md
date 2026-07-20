# Example: wrapping a Go package (coming soon)

> **Status:** The Go toolchain is registered (`go build`), but `GoScanner` doesn't
> parse source yet — running `mcpify` against a `.go` file today throws
> `NotImplementedException`. This doc describes the intended flow.

## 1. The source file

```go
// calculator.go
package calculator

func Add(a int, b int) int {
    return a + b
}

func Subtract(a int, b int) int {
    return a - b
}
```

## 2. Planned invocation

```bash
mcpify ./calculator.go --out ./dist/calculator-mcp --name calculator --transport stdio
```

## 3. Planned toolchain

Once the scanner is implemented, `mcpify` will emit a wrapper Go module exposing each
exported function as an MCP tool, then build it with:

```bash
go build -o ./dist/calculator-mcp/calculator <generated-project>
```

Required executables: `go`.
Supported transports: `stdio`, `http`.

Follow [csharp.md](csharp.md) for a full working walkthrough in the meantime.
