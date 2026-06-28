# Python Demo

A minimal Python math utility wrapped as an MCP server.

## Generate the MCP server

```bash
npx mcpify generate ./math.py --output ./mcp-server
```

## Run it

```bash
cd mcp-server
pip install -r requirements.txt
python3 server.py
```

## Connect from Claude Desktop

Add to `~/Library/Application Support/Claude/claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "python-demo": {
      "command": "python3 /path/to/mcp-server/server.py"
    }
  }
}
```

Claude can then call `add`, `subtract`, `fahrenheit_to_celsius`, and `greet`.
