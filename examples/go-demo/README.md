# Go Demo

A minimal Go math utility wrapped as an MCP server.

## Generate the MCP server

```bash
npx mcpify generate ./math.go --output ./mcp-server
```

## Run it

```bash
cd mcp-server
go mod tidy
go run .
```

## Connect from Claude Desktop

Add to `~/Library/Application Support/Claude/claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "go-demo": {
      "command": "/path/to/mcp-server/math-mcp-server"
    }
  }
}
```

Claude can then call `Add`, `Subtract`, `FahrenheitToCelsius`, and `Greet`.
