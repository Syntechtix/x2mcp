# Example: wrapping a Rust crate (coming soon)

> **Status:** The Rust toolchain is registered (`cargo build`), but `RustScanner`
> doesn't parse source yet — running `x2mcp` against a `.rs` file today throws
> `NotImplementedException`. This doc describes the intended flow.

## Prerequisites

- [Rust toolchain](https://www.rust-lang.org/tools/install) — `cargo` must be on
  your `PATH` so `x2mcp` can shell out to `cargo build --release`. No minimum
  Rust edition/version is pinned yet.

## 1. The source file

```rust
// calculator.rs
pub fn add(a: i32, b: i32) -> i32 {
    a + b
}

pub fn subtract(a: i32, b: i32) -> i32 {
    a - b
}
```

## 2. Planned invocation

```bash
x2mcp ./calculator.rs --out ./dist/calculator-mcp --name calculator --transport stdio
```

## 3. Planned toolchain

Once the scanner is implemented, `x2mcp` will emit a wrapper crate exposing each
public function as an MCP tool, then build it with:

```bash
cargo build --release --manifest-path <generated-project>/Cargo.toml
```

Required executables: `cargo`.
Supported transports: `stdio`, `http`.

Follow [csharp.md](csharp.md) for a full working walkthrough in the meantime.
