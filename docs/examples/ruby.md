# Example: wrapping a Ruby script (coming soon)

> **Status:** The Ruby toolchain is registered (`ruby`, `bundle`), but `RubyScanner`
> doesn't parse source yet — running `mcpify` against a `.rb` file today throws
> `NotImplementedException`. This doc describes the intended flow.

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

## 2. Planned invocation

```bash
mcpify ./calculator.rb --out ./dist/calculator-mcp --name calculator --transport stdio
```

## 3. Planned toolchain

Once the scanner is implemented, `mcpify` will emit a wrapper `server.rb` exposing
each public method as an MCP tool, then launch it with:

```bash
ruby <generated-project>/server.rb
```

Required executables: `ruby`, `bundle`.
Supported transports: `stdio` only (no HTTP transport yet).

Follow [csharp.md](csharp.md) for a full working walkthrough in the meantime.
