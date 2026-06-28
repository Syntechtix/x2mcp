# TypeScript Demo

A minimal TypeScript math utility wrapped as an MCP server.

## Generate the MCP server

```bash
npx mcpify generate ./math.ts --output ./mcp-server
```

## Run it

```bash
cd mcp-server
npm install
npx tsx server.ts
```

## Connect from Claude Desktop

Add to `~/Library/Application Support/Claude/claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "typescript-demo": {
      "command": "npx tsx /path/to/mcp-server/server.ts"
    }
  }
}
```

Claude can then call `add`, `subtract`, `fahrenheitToCelsius`, and `greet`.
