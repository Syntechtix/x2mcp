# x2mcp manual QA walkthrough (all 5 languages)

> **Update (2026-08-19, later same day): all 4 bugs below are fixed.** See "Fix pass" at the
> bottom of this document for what changed, the unit tests added, and the re-verification
> results — every repro in this document was rerun against the fixed CLI and now passes.

**Date:** 2026-08-19
**What this is:** a hands-on pass that goes beyond the repo's unit/integration tests — pack the real CLI, install it as a global tool exactly like an end user would, run it against the exact "calculator" sample from each `docs/examples/*.md`, then speak real MCP JSON-RPC (`initialize` → `tools/list` → `tools/call`) to the binary it produces, the same way Claude Desktop or VS Code would.

**Result:** 4 bugs found, 2 of them release-blocking. Go and Python passed clean. Rust and Ruby passed with caveats. C# is currently broken for real clients.

---

## Setup

1. `dotnet pack src/X2Mcp.Cli/X2Mcp.Cli.csproj -c Release -o ./local-nupkg`
2. `dotnet tool install --global --add-source ./local-nupkg x2mcp` → installs the real `x2mcp` command, matching the README's own install instructions.
3. Confirmed `x2mcp --help` matches the documented CLI surface (`<source>`, `--out-dir`, `--name`, `--transport`).
4. Installed per-language prerequisites: Python `mcp` + `pyinstaller`, Go 1.25 (the generated Go wrapper's `go-sdk` dependency requires ≥1.25), Rust/cargo, Ruby — all per the docs' "Prerequisites" sections.

For each language below, the source sample is copied verbatim from that language's `docs/examples/*.md`, then run through the real installed `x2mcp` CLI, then the resulting binary is driven with a small script that opens it as a subprocess and exchanges real MCP JSON-RPC messages.

---

## Bugs found

### Bug 1 — `--out-dir` (and the default output path) silently resolves to the wrong place — **all 5 languages, critical**

Reproduce: `cd calculator && x2mcp ./Calculator --out-dir ./dist/calculator-mcp --name calculator` — i.e. exactly the command shown in every single `docs/examples/*.md` file.

What happens: the CLI prints `Done. MCP server written to: ./dist/calculator-mcp` and exits 0, but nothing is written there. The real build output lands under the internal temp wrapper folder instead — `/tmp/x2mcp/calculator/dist/calculator-mcp` on Linux/macOS (`%TEMP%\x2mcp\calculator\dist\calculator-mcp` on Windows). This also hits the **default** output path (`./dist/<name>-mcp`, used whenever `--out-dir` is omitted) — so it affects the simplest possible invocation, not just an edge case.

Root cause: `src/X2Mcp.Cli/Program.cs` never resolves `--out-dir` (or the computed default) to an absolute path before passing it down. `OrchestrationEngine.RunAsync` then runs the toolchain's build/publish command with its working directory set to the generated wrapper project's temp folder, so the `{OutputPath}` token in each `toolchain.json`'s `publishCommand` resolves relative to *that* folder, not the directory the user ran `x2mcp` from.

Suggested fix: in `Program.cs`, call `Path.GetFullPath(outputPath)` right after computing `outputPath`, before it's handed to `OrchestrationEngine`.

Every result below was produced by working around this manually (passing an absolute `--out-dir`) — without that workaround, none of the 5 languages' documented example commands actually produce a server where they claim to.

### Bug 2 — C# / `--transport stdio`: generated server's logging corrupts the MCP protocol stream — **critical, currently unusable**

Reproduce: build the C# `Calculator` example exactly as in `docs/examples/csharp.md`, run the resulting binary, send it any JSON-RPC message on stdin.

What happens: the very first bytes on stdout are Microsoft.Extensions.Hosting's default console log lines (`info: ModelContextProtocol.Server.StdioServerTransport[...] Server (stream) (calculator.mcp) transport reading messages.`, etc.) — not JSON-RPC. Any real MCP client (Claude Desktop, VS Code, the MCP Inspector) reads stdout expecting only protocol frames; the first line it gets back is unparseable log text, so the connection fails immediately. I confirmed this directly: piping a well-formed `initialize` request to the built binary and capturing stdout/stderr separately shows the log lines on stdout and nothing on stderr — the actual JSON-RPC response is never distinguishable from the log noise.

Root cause: `DotNetWrapperEmitter.GenerateProgram`'s stdio branch uses `Host.CreateApplicationBuilder(args)` with default logging configuration, which registers a console logger writing to stdout. The official ModelContextProtocol C# SDK's own samples explicitly call out that stdio-transport servers must clear default logging providers or redirect them to stderr for exactly this reason.

Suggested fix: in the stdio branch of `GenerateProgram`, emit `builder.Logging.ClearProviders();` (or `builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);`) before `AddMcpServer()`.

**Impact: every C#-wrapped MCP server built with `--transport stdio` today is non-functional with a real client.** This is the highest-priority fix before calling language support "done."

### Bug 3 — Ruby: schema-honoring clients get silently wrong results — **high, silent correctness bug**

Reproduce: build the Ruby `Calculator` example, call `Calculator_add` with the arguments the tool's own advertised JSON Schema calls for (`{"a": "5", "b": "7"}`, since every parameter is typed `"string"`).

What happens: returns `"57"` (string concatenation) instead of `12`. If you instead pass JSON numbers (bypassing what the schema actually asks for), it returns the correct `12` — the underlying `add` method is fine, but the tool's own contract is wrong.

Root cause: the Ruby scanner has no static type information to work from, so `RubyWrapperEmitter` types every parameter as `"string"` in the generated JSON Schema. Ruby's `+` then does whatever the passed-in types support — string concatenation for strings, numeric addition for numbers — so a schema-compliant caller gets silently wrong answers for anything arithmetic.

Not a crash, so it wouldn't be caught by a test that calls the Ruby method directly — only visible when you actually go through the MCP schema contract as a real client would. Recommend either inferring types with a lightweight heuristic (e.g. sniff default values or a `# @param` comment convention) or clearly documenting in `docs/examples/ruby.md` that generated tools' schemas are string-only today and results for arithmetic-style methods depend on caller behavior.

### Bug 4 — Rust: the documented example command fails to build as written — **medium, docs/UX**

Reproduce: `docs/examples/rust.md`'s own example — a crate named `calculator` (in `Cargo.toml`), wrapped with `x2mcp ./calculator --name calculator`.

What happens: `cargo install` fails with `package collision in the lockfile: packages calculator v0.1.0 (.../calculator) and calculator v0.1.0 (/tmp/x2mcp/calculator) are different, but only one can be written to lockfile unambiguously`.

Root cause: `--name` defaults to (and the doc's example explicitly sets it to) the same name as the source crate's own `Cargo.toml` package. The generated wrapper crate and the source crate then collide as two different packages with the identical name in one Cargo build graph.

Workaround: pick a `--name` different from the source crate's package name (confirmed working — `--name calcmcp` against the identical source built and ran cleanly, add(5,7) → 12, correct `int32`/`double` schema types).

Suggested fix: either have `x2mcp` detect `--name == source crate name` up front and fail fast with a clear message, or auto-suffix the generated crate's package name (e.g. `{name}-mcp-wrapper`) so it can never collide.

### Minor / cosmetic

- Rust-generated server reports `serverInfo.name` as `"rmcp"` (the SDK crate's own name) rather than the configured server name — only visible in an MCP client's server list, not functionally significant.

---

## Per-language results

| Language | Built OK (with absolute `--out-dir`) | `initialize` | `tools/list` (correct schema) | `tools/call` (correct result) | Verdict |
|---|---|---|---|---|---|
| **Go** | ✅ | ✅ | ✅ (int/float types, error mapping per docs' table) | ✅ `Add(5,7)=12` | **Pass** |
| **Python** | ✅ | ✅ | ✅ (all 4 tools: 2 top-level fns + class method + async method) | ✅ `add(5,7)=12` | **Pass** |
| **Rust** | ✅ (once `--name` ≠ crate name — Bug 4) | ✅ | ✅ (`int32`/`double` types) | ✅ `add(5,7)=12` | **Pass with caveat** |
| **Ruby** | ✅ | ✅ | ✅ but all params typed `string` | ⚠️ wrong for schema-compliant string args (Bug 3) | **Pass with correctness bug** |
| **C#** | ✅ (compiles/publishes fine) | ❌ stdout corrupted by logs (Bug 2) | — | — | **Fails for real clients** |

---

## Recommendation before calling this release-ready

Bug 1 and Bug 2 are release blockers: Bug 1 means every documented example silently fails to place output where it says it does (across all 5 languages), and Bug 2 means the flagship C#/.NET path — the language this tool's own generated servers are written in — doesn't work with a real MCP client at all. Bug 3 (Ruby) and Bug 4 (Rust) are narrower but still worth fixing or at minimum documenting before a "full publish," since Bug 4 breaks the Rust doc's example exactly as written and Bug 3 produces silently wrong answers rather than an obvious failure.

---

## Fix pass (same day)

All 4 bugs were fixed, unit-tested, and re-verified live against the rebuilt CLI using the exact same repro steps as above.

| Bug | Fix | File | New/updated test |
|---|---|---|---|
| 1 — `--out-dir` resolves to the wrong place | `Program.cs` now calls `Path.GetFullPath` on the output path before handing it to `OrchestrationEngine`, so it's absolute before the toolchain's working-directory switch can misinterpret it | `src/X2Mcp.Cli/Program.cs` | No `X2Mcp.Cli` test project exists; re-verified live instead (see below) |
| 2 — C# stdio server's logs corrupt the protocol stream | `builder.Logging.ClearProviders()` added to the generated stdio `Program.cs`, so stdout carries only JSON-RPC frames | `src/X2Mcp.Language.DotNet/DotNetWrapperEmitter.cs` | `DotNetWrapperEmitterTests.Emit_StdioTransport_ClearsDefaultLoggingProviders` |
| 3 — Ruby schema falsely claims every param is a string | Generated schema no longer declares a type for parameters it can't actually infer (`{}` instead of `{ type: 'string' }`), so it no longer misleads schema-honoring clients into sending the wrong shape | `src/X2Mcp.Language.Ruby/RubyWrapperEmitter.cs` | `RubyWrapperEmitterTests.Emit_Server_ParameterSchema_DoesNotClaimStringType` |
| 4 — Rust wrapper name collides with source crate | Generated wrapper's `[package]` name is now always suffixed (`{name}-mcp-server`), distinct from the source crate's name; `[[bin]]` — and so the actual output binary's filename — is unaffected | `src/X2Mcp.Language.Rust/RustWrapperEmitter.cs` | `RustWrapperEmitterTests.Emit_ServerNameMatchesSourceCrateName_PackageNameDoesNotCollide` |

**Test results:** all 3 touched unit test projects pass in full — DotNet 52/52, Ruby 46/46, Rust 61/61 (each including its new regression test) — and the modified classes (`DotNetWrapperEmitter`, `RubyWrapperEmitter`, `RustWrapperEmitter`) sit at 100% line/branch coverage, matching the repo's CI requirement. `dotnet format --verify-no-changes` passes on every touched project.

**Live re-verification (rebuilt CLI, same repro commands as the original report):**

- **Bug 1:** `x2mcp ./Calculator/Calculator.csproj --out-dir ./dist/calculator-mcp --name calculator` (relative path, exactly as every doc shows it) now correctly writes to `<cwd>/dist/calculator-mcp` — confirmed for C#, and spot-checked on Go and Python (untouched by the language-specific fixes, confirming the shared fix didn't regress them).
- **Bug 2:** the rebuilt C# calculator server now completes a full `initialize` → `tools/list` → `tools/call` handshake over stdio with clean JSON — `Add(5, 7)` → `12`, all 3 tools correctly listed.
- **Bug 3:** the schema no longer advertises `type: 'string'`; a caller sending numeric arguments — the natural choice for an untyped `add` tool — now gets `12`. Note this is an inherent Ruby limitation, not something further code can fully close: without real type information, whether a given call is numeric or stringy is still ultimately up to the caller. The fix removes the schema's previous false claim rather than papering over the underlying lack of static types; `docs/examples/ruby.md` now says so explicitly.
- **Bug 4:** the Rust doc's own example command (`--name calculator` against a crate named `calculator`) now builds successfully in ~72s and runs correctly — `add(5, 7)` → `12`, `serverInfo` etc. all as expected.

Docs updated to match: `docs/examples/csharp.md` (shown `Program.cs` now includes `ClearProviders()`), `docs/examples/rust.md` (shown `Cargo.toml` now shows the suffixed package name, with an explanatory note), `docs/examples/ruby.md` (added a note on parameter-typing behavior).
