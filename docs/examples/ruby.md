# Example: wrapping a Ruby script

## How scanning works

`x2mcp` produces a single MCP server from whatever source you give it.

- **Directory** (recommended) — recursively scans all `.rb` files in the directory
  and every subfolder. All discovered class methods are registered as tools in a
  single server.
- **Single `.rb` file** — wraps only the classes in that one file.

Files matching `test_*.rb` or `*_test.rb` and anything inside `test/` or `spec/`
subdirectories are always skipped.

## Prerequisites

- Ruby on your `PATH`.
- Ruby support currently targets `stdio` transport.

## 1. The source file

```ruby
# calculator.rb
class Calculator
  def add(a, b)
    a + b
  end

  def subtract(a, b)
    a - b
  end
end
```

## 2. Run x2mcp

Point at the directory (whole library) or a single file:

```bash
# recommended: whole library directory
x2mcp ./calculator --out-dir ./dist/calculator-mcp --name calculator --transport stdio

# or: a single file
x2mcp ./calculator.rb --out-dir ./dist/calculator-mcp --name calculator --transport stdio
```

## 3. What gets generated

`x2mcp` emits a `server.rb` wrapper, a `build.rb` packager, and source `.rb` files
needed by the wrapper.

The wrapper loads copied source files and exposes module functions and class methods
as tools over stdio JSON-RPC.

```ruby
require 'json'
require_relative './calculator'

TOOLS = {
  'Calculator_add' => {
    params: [{ name: 'a', optional: false }, { name: 'b', optional: false }],
    call: lambda { |args| (@instance_calculator ||= Object.const_get('Calculator').new).send(:add, *[args['a'], args['b']]) }
  },
  'Calculator_subtract' => {
    params: [{ name: 'a', optional: false }, { name: 'b', optional: false }],
    call: lambda { |args| (@instance_calculator ||= Object.const_get('Calculator').new).send(:subtract, *[args['a'], args['b']]) }
  }
}
```

**A note on parameter types:** Ruby has no static type annotations for `x2mcp` to read, so the JSON Schema advertised for each tool's parameters declares no type — the JSON value a caller sends is passed straight through to the wrapped method exactly as received (a JSON number arrives as a Ruby `Integer`/`Float`, a JSON string arrives as a Ruby `String`). For a method like `add` that relies on Ruby's `+`, that means the caller — not the schema — determines whether you get numeric addition or string concatenation. If precise typing matters for your tool, consider coercing/validating argument types inside the wrapped method itself.

## 4. Build

`x2mcp` runs the Ruby toolchain's publish step through the generated `build.rb`:

```bash
ruby <generated-project>/build.rb ./dist/calculator-mcp calculator
```

This produces:

- `./dist/calculator-mcp/calculator` (or `calculator.cmd` on Windows) launcher
- `./dist/calculator-mcp/calculator_bundle/` containing `server.rb` and copied source

## 5. `--transport http`

Ruby currently supports `stdio` only. Passing `--transport http` is not supported
for Ruby modules yet.

## 6. Connect to an MCP client

Once `./dist/calculator-mcp/` exists, register the server in any MCP client. Ruby
only supports **stdio** transport.

> **Absolute paths required.** MCP clients launch the launcher as a subprocess;
> relative paths won't resolve. Replace the placeholder paths below with the real
> absolute path on your machine.

### Claude Desktop

- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
- Linux: `~/.config/Claude/claude_desktop_config.json`
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "calculator": {
      "command": "C:/absolute/path/to/dist/calculator-mcp/calculator.cmd",
      "args": []
    }
  }
}
```

Restart Claude after saving.

### ChatGPT Codex

- macOS / Linux: `~/.codex/config.toml`
- Windows: `%USERPROFILE%\.codex\config.toml`

```toml
[[mcp_servers]]
name = "calculator"
cmd = ["C:/absolute/path/to/dist/calculator-mcp/calculator.cmd"]
```

### VS Code

- macOS / Linux / Windows: `.vscode/mcp.json` (workspace root)

```json
{
  "servers": {
    "calculator": {
      "type": "stdio",
      "command": "${workspaceFolder}/dist/calculator-mcp/calculator.cmd"
    }
  }
}
```

The file is picked up automatically — no restart needed. Switch to **Agent mode** and
the calculator tools are available immediately.
