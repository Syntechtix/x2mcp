# Example: wrapping a Python module

## How scanning works

`x2mcp` produces a single MCP server from whatever source you give it.

- **Directory** (recommended) — recursively scans all `.py` files in the directory
  and every subfolder. All discovered top-level functions and class methods are
  registered as tools in a single server.
- **Single `.py` file** — wraps only the functions and classes in that one file.

Files matching `test_*.py` or `*_test.py` and anything inside `__pycache__` are
always skipped.

## Prerequisites

- Python 3 and [PyInstaller](https://pyinstaller.org/) on your `PATH`.
- The Python MCP SDK:

```bash
python -m pip install mcp
```

## 1. The source module

```python
# calculator.py
def add(a: int, b: int) -> int:
  return a + b


def greet(name: str) -> str:
  return f"Hello, {name}"


class Calculator:
  def multiply(self, a: int, b: int) -> int:
    return a * b

  async def echo(self, value: str) -> str:
    return value
```

## 2. Run x2mcp

Point at the directory (whole module) or a single file:

```bash
# recommended: whole module directory
x2mcp ./calculator --out-dir ./dist/calculator-mcp --name calculator --transport stdio

# or: a single file
x2mcp ./calculator.py --out-dir ./dist/calculator-mcp --name calculator --transport stdio
```

## 3. What gets generated

`x2mcp` emits a `main.py` wrapper plus the source `.py` files the wrapper imports.
The wrapper uses FastMCP and registers top-level functions and class methods as tools.

```python
from mcp.server.fastmcp import FastMCP
import calculator

mcp = FastMCP("calculator")

mcp.tool(name="add")(calculator.add)
mcp.tool(name="greet")(calculator.greet)
_calculator_Calculator = calculator.Calculator()
mcp.tool(name="multiply")(_calculator_Calculator.multiply)
mcp.tool(name="echo")(_calculator_Calculator.echo)

if __name__ == "__main__":
  mcp.run(transport="stdio")
```

## 4. Build

`x2mcp` resolves the toolchain executable from `requiredExecutables`
(`pyinstaller`) and then runs the publish args:

```bash
pyinstaller --onefile --name calculator --distpath ./dist/calculator-mcp <generated-project>/main.py
```

## 5. `--transport http`

Passing `--transport http` switches the entrypoint to streamable HTTP:

```python
if __name__ == "__main__":
  mcp.run(transport="streamable-http")
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

FastMCP listens on `http://localhost:8000` by default. Use `http://localhost:8000/mcp`
in the HTTP configs below.

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
      "url": "http://localhost:8000/mcp"
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
url = "http://localhost:8000/mcp"
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
      "url": "http://localhost:8000/mcp"
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
      "url": "http://localhost:8000/mcp"
    }
  }
}
```
