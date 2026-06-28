# mcpify

> Create a native-language MCP server wrapper around any codebase.

`mcpify` reads your existing source code, extracts its exported functions, and generates a fully working [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server in the **same language** as your code — so AI models like Claude can call your functions directly, with no subprocess overhead.

[![CI](https://github.com/Syntechtix/mcpify/actions/workflows/ci.yml/badge.svg)](https://github.com/Syntechtix/mcpify/actions/workflows/ci.yml)

---

## Quick start

```bash
# Generate an MCP server from a TypeScript file
npx mcpify generate ./src/myFunctions.ts

# … or a Python file
npx mcpify generate ./utils/math.py

# … or a Go file
npx mcpify generate ./pkg/tools/tools.go
```

The generated server is written to `./mcp-server/` by default.

---

## Supported languages

| Language | Extraction | Generation | SDK used |
|---|---|---|---|
| TypeScript / JavaScript | TypeScript compiler API | `@modelcontextprotocol/sdk` | Native |
| Python | `ast` module | `mcp` (PyPI) | Native |
| Go | `go/ast` | `github.com/mark3labs/mcp-go` | Native |

---

## Installation

```bash
npm install -g mcpify       # global CLI
# or use without installing:
npx mcpify generate <path>
```

---

## CLI reference

### `mcpify generate <path>`

Analyse `<path>` (file or directory), extract exported functions, and write a ready-to-run MCP server.

```
Options:
  -l, --language <lang>   Force language: typescript | python | go
  -o, --output <dir>      Output directory  (default: <path>/../mcp-server)
  -c, --config <file>     Path to mcpify.config.json
```

### `mcpify init <path>`

Same as `generate`, with extra onboarding instructions printed to the console.

### `mcpify run <path>`

Generate *and* immediately start the MCP server on stdio. Useful for quickly testing a wrapped module.

---

## Configuration

Place an optional `mcpify.config.json` at your project root:

```json
{
  "serverName": "my-tools",
  "serverVersion": "1.0.0",
  "outputDir": "./generated/mcp-server",
  "language": "typescript",
  "include": ["src/tools/**/*.ts"],
  "exclude": ["**/*.test.ts"],
  "toolDescriptions": {
    "myFunction": "Override the auto-detected description"
  }
}
```

| Field | Type | Description |
|---|---|---|
| `serverName` | `string` | Name embedded in the generated MCP server metadata |
| `serverVersion` | `string` | Version embedded in the server metadata |
| `outputDir` | `string` | Directory where the server is generated |
| `language` | `string` | Force language detection |
| `include` | `string[]` | Glob patterns for source files to include |
| `exclude` | `string[]` | Glob patterns to exclude |
| `toolDescriptions` | `object` | Override auto-detected descriptions by tool name |

---

## Programmatic API

`mcpify` can also be used as a library:

```typescript
import { Orchestrator, TypeScriptAdapter, PythonAdapter } from "mcpify";

const orch = new Orchestrator()
  .register(new TypeScriptAdapter())
  .register(new PythonAdapter());

const serverFile = await orch.run("./src/math.ts", {
  outputDir: "./mcp-server",
});
console.log("Generated:", serverFile);
```

---

## Examples

| Language | Source | Instructions |
|---|---|---|
| TypeScript | [`examples/typescript-demo/`](examples/typescript-demo/) | [README](examples/typescript-demo/README.md) |
| Python | [`examples/python-demo/`](examples/python-demo/) | [README](examples/python-demo/README.md) |
| Go | [`examples/go-demo/`](examples/go-demo/) | [README](examples/go-demo/README.md) |

---

## How it works

```
Source file(s)
      │
      ▼
┌─────────────┐    ┌────────────────────┐
│  Extractor  │───▶│  ToolDefinition[]  │
│ (per-lang)  │    │  (JSON Schema IR)  │
└─────────────┘    └────────────────────┘
                            │
                            ▼
                   ┌─────────────────┐
                   │    Generator    │
                   │  (per-lang)     │
                   └────────┬────────┘
                            │
                            ▼
                   MCP server (server.ts /
                   server.py / main.go)
```

1. **Extractor** — Parses the source file using its native AST tooling and returns a list of `ToolDefinition` objects (name, description, JSON Schema).
2. **Generator** — Takes the `ToolDefinition[]` and emits a complete MCP server file that imports the original module and registers each function as an MCP tool.

---

## Adding a custom language adapter

Implement the `LanguageAdapter` interface and register it with the `Orchestrator`:

```typescript
import { LanguageAdapter, MCPServerSpec, ToolDefinition, Orchestrator } from "mcpify";

class RubyAdapter implements LanguageAdapter {
  readonly name = "ruby";
  detect(filePath: string) { return filePath.endsWith(".rb"); }
  async extract(filePath: string): Promise<ToolDefinition[]> { /* ... */ }
  async generate(spec: MCPServerSpec, outputDir: string): Promise<string> { /* ... */ }
}

const orch = new Orchestrator().register(new RubyAdapter());
```

---

## Development

```bash
git clone https://github.com/Syntechtix/mcpify
cd mcpify
npm install
npm run build
npm test
```

Requirements for running all tests:
- Node.js ≥ 18
- Python 3.8+
- Go 1.21+

---

## License

MIT
