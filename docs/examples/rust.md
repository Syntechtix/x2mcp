# Example: wrapping a Rust crate

`x2mcp` scans a Rust crate with a lightweight regex-based parser, finds every
`pub fn` — both free functions and methods on `pub struct`s — and generates a
companion binary crate that depends on your original crate and exposes those
functions as MCP tools.

## How scanning works

`x2mcp` produces a single MCP server from whatever source you give it.

- **Directory** (recommended) — recursively scans all `.rs` files in the directory
  and every subfolder. This is the normal way to wrap a whole crate.
- **Single `.rs` file** — wraps only the `pub fn`s in that one file.

Files under a `tests/` directory (Cargo's integration-test convention) are always
skipped.

Two kinds of items are picked up:

- **Free functions** — any top-level `pub fn`, grouped together as a single set of
  tools.
- **Struct methods** — `pub fn`s inside `impl StructName { ... }`, but only when
  `StructName` is itself declared `pub struct`. Methods on a private struct are
  skipped, matching how the Go scanner skips methods with an unexported receiver
  type. Each wrapped struct is instantiated via `StructName::default()`, so wrapped
  structs must implement (or derive) `Default`.

A `pub fn`'s parameters and return type are read directly from its signature.
`Option<T>` parameters are treated as optional. Return values are formatted with
`{:?}` (`Debug`), so wrapped types must implement (or derive) `Debug`.

If your crate's package name contains hyphens (e.g. `my-lib`), `x2mcp` uses the
hyphenated name in the generated `Cargo.toml` dependency line but the
underscored form (`my_lib`) everywhere the crate is referenced in generated Rust
code — the same convention Cargo itself uses.

## Prerequisites

- [Rust toolchain](https://www.rust-lang.org/tools/install) — `cargo` must be on
  your `PATH` so `x2mcp` can shell out to `cargo build` and `cargo install`.
- Your source file/directory must live inside a Cargo package — a `Cargo.toml` with
  a `[package]` section must exist in the source directory or one of its parent
  directories, since `x2mcp` reads it to resolve your crate's name and wires it in
  as a local path dependency.

## 1. The source crate

```
calculator/
├── Cargo.toml
└── src/
    └── lib.rs
```

```toml
# Cargo.toml
[package]
name = "calculator"
version = "0.1.0"
edition = "2021"
```

```rust
// src/lib.rs
pub fn add(a: i32, b: i32) -> i32 {
    a + b
}

pub fn divide(a: f64, b: f64) -> Result<f64, String> {
    if b == 0.0 {
        return Err("division by zero".to_string());
    }
    Ok(a / b)
}
```

Only `pub fn`s are picked up — private functions, methods on private structs, and
anything under `tests/` are skipped.

## 2. Run x2mcp

Point at the crate directory (whole crate) or a single file:

```bash
# recommended: whole crate directory
x2mcp ./calculator --out-dir ./dist/calculator-mcp --name calculator --transport stdio

# or: a single file
x2mcp ./calculator/src/lib.rs --out-dir ./dist/calculator-mcp --name calculator --transport stdio
```

- `--out-dir` — where the built, runnable MCP server binary ends up
- `--name` — the server name (also used for the generated project's temp folder,
  and sanitized into the generated crate/binary name)
- `--transport` — `stdio` (default) or `http`

## 3. What gets generated

`x2mcp` writes a temporary wrapper crate (under `%TEMP%/x2mcp/<name>` /
`/tmp/x2mcp/<name>`) that looks like this:

```
calculator/
├── Cargo.toml
└── src/
    └── main.rs
```

**Cargo.toml** — depends on the MCP Rust SDK (`rmcp`) and your original crate,
referenced by local path so no publishing is required:

```toml
[package]
name = "calculator"
version = "0.1.0"
edition = "2021"

[[bin]]
name = "calculator"
path = "src/main.rs"

[dependencies]
rmcp = { version = "3", features = ["server", "transport-io", "transport-streamable-http-server"] }
tokio = { version = "1", features = ["macros", "rt-multi-thread"] }
schemars = "0.8"
serde = { version = "1", features = ["derive"] }
anyhow = "1"
calculator = { path = "../../../calculator" }
```

**src/main.rs** — one generated `{Function}Params` struct per function plus a
`#[tool]`-annotated method that calls straight into your original crate:

```rust
use rmcp::{handler::server::wrapper::Parameters, schemars, tool, tool_router, ServiceExt};
use rmcp::transport::stdio;
use calculator;

#[derive(Debug, serde::Deserialize, schemars::JsonSchema)]
struct AddParams {
    a: i32,
    b: i32,
}

#[derive(Debug, serde::Deserialize, schemars::JsonSchema)]
struct DivideParams {
    a: f64,
    b: f64,
}

#[derive(Clone)]
struct GeneratedTools;

#[tool_router(server_handler)]
impl GeneratedTools {
    #[tool(description = "add")]
    fn add(&self, Parameters(AddParams { a, b }): Parameters<AddParams>) -> String {
        let result = calculator::add(a, b);
        format!("{:?}", result)
    }

    #[tool(description = "divide")]
    fn divide(&self, Parameters(DivideParams { a, b }): Parameters<DivideParams>) -> String {
        let result = calculator::divide(a, b);
        format!("{:?}", result)
    }
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let service = GeneratedTools.serve(stdio()).await?;
    service.waiting().await?;
    Ok(())
}
```

Every generated tool method returns a `String`: functions with a return type have
their result formatted with `{:?}` (`Debug`); functions with no return type run for
their side effect and return `"ok"`.

## 4. Build

`x2mcp` then runs `cargo install` for you, which builds in release mode and places
the binary directly in `--out-dir`:

```bash
cargo install --path <generated-project> --root ./dist/calculator-mcp --force
```

Unlike the other language wrappers, `cargo install` nests the binary under a `bin/`
subfolder — the runnable server ends up at `./dist/calculator-mcp/bin/calculator`
(`.exe` on Windows), not directly in `./dist/calculator-mcp/`.

## 5. `--transport http`

Passing `--transport http` instead drops the stdio transport and serves MCP over a
stateless streamable HTTP handler on port 8080, using `rmcp`'s
`StreamableHttpService` behind a minimal `axum` router:

```rust
let http_service = StreamableHttpService::new(
    || Ok(GeneratedTools),
    Arc::new(LocalSessionManager::default()),
    StreamableHttpServerConfig::default(),
);
let router = Router::new().fallback_service(tower::service_fn(move |req| {
    let http_service = http_service.clone();
    async move { Ok::<_, std::convert::Infallible>(http_service.handle(req).await) }
}));
let listener = tokio::net::TcpListener::bind("0.0.0.0:8080").await?;
axum::serve(listener, router).await?;
```

The generated `Cargo.toml` additionally depends on `axum` and `tower` in this mode.

## 6. Connect to an MCP client

Once `./dist/calculator-mcp/bin/calculator` exists, register the server in any MCP
client.

> **Absolute paths required.** MCP clients launch the binary as a subprocess; relative
> paths won't resolve. Replace the placeholder paths below with the real absolute path
> on your machine.

### Starting the HTTP server

If you built with `--transport http`, start the binary before configuring any client:

```bash
./dist/calculator-mcp/bin/calculator
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
      "command": "/absolute/path/to/dist/calculator-mcp/bin/calculator",
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
cmd = ["/absolute/path/to/dist/calculator-mcp/bin/calculator"]
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
      "command": "${workspaceFolder}/dist/calculator-mcp/bin/calculator"
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
      "command": "C:/absolute/path/to/dist/calculator-mcp/bin/calculator.exe"
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
