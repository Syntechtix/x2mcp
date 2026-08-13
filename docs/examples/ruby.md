# Example: wrapping a Ruby script

## Prerequisites

- Ruby and [Bundler](https://bundler.io/) on your `PATH`.
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

```bash
x2mcp ./calculator.rb --out ./dist/calculator-mcp --name calculator --transport stdio
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
>
> On Windows the launcher is `calculator.cmd`; on macOS/Linux it is `calculator`
> (no extension). Windows clients can't execute `.cmd` files directly as a subprocess
> command — wrap them with `cmd /c` as shown below.

### Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or
`%APPDATA%\Claude\claude_desktop_config.json` (Windows):

**macOS / Linux:**

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

**Windows:**

```json
{
  "mcpServers": {
    "calculator": {
      "command": "cmd",
      "args": ["/c", "C:\\absolute\\path\\to\\dist\\calculator-mcp\\calculator.cmd"]
    }
  }
}
```

Restart Claude after saving.

### ChatGPT Codex

Edit `~/.codex/config.toml`:

**macOS / Linux:**

```toml
[[mcp_servers]]
name = "calculator"
cmd = ["/absolute/path/to/dist/calculator-mcp/calculator"]
```

**Windows:**

```toml
[[mcp_servers]]
name = "calculator"
cmd = ["cmd", "/c", "C:/absolute/path/to/dist/calculator-mcp/calculator.cmd"]
```

### VS Code

Create or edit `.vscode/mcp.json` in your workspace root:

**macOS / Linux:**

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

**Windows:**

```json
{
  "servers": {
    "calculator": {
      "type": "stdio",
      "command": "cmd",
      "args": ["/c", "${workspaceFolder}/dist/calculator-mcp/calculator.cmd"]
    }
  }
}
```

The file is picked up automatically — no restart needed. Switch to **Agent mode** and
the calculator tools are available immediately.

### Visual Studio 2022

Visual Studio 2022 17.14+ reads the same `.vscode/mcp.json` format. Create the file
at the solution root (next to your `.sln` / `.slnx`):

```json
{
  "servers": {
    "calculator": {
      "type": "stdio",
      "command": "cmd",
      "args": ["/c", "C:/absolute/path/to/dist/calculator-mcp/calculator.cmd"]
    }
  }
}
```

Restart Visual Studio after creating or modifying the file. Tools appear in Agent mode.
