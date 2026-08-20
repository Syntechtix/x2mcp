# AGENTS.md

Guidance for coding agents working in this repository.

## Git

- Never run `git commit` (or `git push`) — the user commits everything themselves, manually, always. Staging changes (`git add`) or inspecting status/diffs is fine; committing is not.

## Stack

- C# 10, .NET 10 (`net10.0`), solution file is `X2Mcp.slnx` (not a `.sln`).
- `Directory.Build.props` sets `Nullable=enable`, `TreatWarningsAsErrors=true`, `IsPackable=false` (overridden per-project where packaging is needed).
- The entire codebase is C# — the `X2Mcp.Language.*` projects are C# scanners/emitters that parse/target other languages (Go, Python, Rust, Ruby) as *output*, not the implementation language.

## Build/test

```
dotnet restore X2Mcp.slnx
dotnet build X2Mcp.slnx -c Release
dotnet test X2Mcp.slnx -c Release
```

Unit tests only (exclude E2E/integration tests that require external toolchains on PATH):

```
dotnet test X2Mcp.slnx --filter "Category!=Integration" -c Release
```

Integration/E2E tests are tagged `[Trait("Category", "Integration")]` and require the target language's toolchain installed (e.g. `go` for `GoEndToEndTests`, `dotnet` itself for `DotNetEndToEndTests`):

```
dotnet test X2Mcp.slnx --filter "Category=Integration" -c Release
```

## Conventions

- Names do the documenting, not comments. Choose variable, method, and property names precise enough that the code needs no comment to say what it is or does — `retryDelayMs = 4000`, not `x = 4000 // retry delay in ms`. No comments except one-liners stating what the code genuinely can't show on its own (a non-obvious *why*, an upstream constraint, a gotcha) — never a comment that just restates what a well-named line already says.
- Don't invent behavior — verify assumptions against the actual implementation (existing scanners/emitters, SDK docs) before writing code; don't guess and implement a guess.
- `TreatWarningsAsErrors=true` — nullable warnings and analyzer warnings fail the build; don't suppress, fix them.
- Windows/WSL dual restore: restoring from WSL bash writes Linux-style NuGet paths into `obj/project.assets.json`, breaking Windows-side IntelliSense. Restore via `powershell.exe -NoProfile -Command "dotnet restore X2Mcp.slnx"` from WSL when this happens.

## Lint

CI runs `dotnet format X2Mcp.slnx --verify-no-changes` — run it locally before committing, since a formatting-only diff fails the build:

```
dotnet format X2Mcp.slnx --verify-no-changes
```

## Test coverage

**100% line coverage is required** on all unit-tested code. CI (`build-and-unit-test` job in [.github/workflows/build.yml](.github/workflows/build.yml)) collects coverage on every push/PR, posts a Markdown summary to the job's Summary tab, and **fails the build if line coverage is below 100%**. Run the same check locally after every code change, before considering it done/pushable:

```
dotnet test X2Mcp.slnx --filter "Category!=Integration" -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage
reportgenerator -reports:"coverage/**/coverage.cobertura.xml" -targetdir:coveragereport -reporttypes:"TextSummary" -classfilters:"-System.Text.RegularExpressions.Generated*"
cat coveragereport/Summary.txt
```

(`reportgenerator` is a .NET global tool: `dotnet tool install -g dotnet-reportgenerator-globaltool`. The `-classfilters` flag excludes compiler-generated regex source, which isn't meaningfully testable.)

- **Coverage must come from exercising real public entry points** (constructors, public methods) — not reflection into private/internal members. Reflection-based test access is disallowed except in genuinely unavoidable cases, which must be justified in the test/PR.
- If a private/internal code path can't be reached through a public API, that's a design smell to fix (extract a testable public seam, restructure the method) — not a reason to reach around encapsulation with reflection.
- `coverage/` and `coveragereport/` are gitignored — never commit generated coverage artifacts.

## GitHub Actions workflows

- [.github/workflows/build.yml](.github/workflows/build.yml) — runs on `push`/`pull_request` to `main`. Jobs: `build-and-unit-test` (format check, build, unit tests, coverage collection + 100% line-coverage gate + summary), `integration-dotnet`, `integration-go` (installs the Go toolchain), `go-fixtures-lint` (path-filtered `gofmt` check), `all-checks-passed` (required-status gate — fails if any of the above jobs fail).
- [.github/workflows/publish.yml](.github/workflows/publish.yml) — manual `workflow_dispatch` only. Inputs: `bump` (patch/minor/major) and an optional freeform `suffix` (e.g. `beta.1`) — always combinable. Computes the next SemVer tag from the latest `vX.Y.Z` git tag (falls back to `v0.0.0` if none exist), tags + pushes the release, packs `X2Mcp.Cli` as a tool, and publishes to NuGet.org via **Trusted Publishing** (OIDC `NuGet/login` action) — no long-lived `NUGET_API_KEY` secret is used; requires a Trusted Publishing policy configured on nuget.org for this repo (`Syntechtix/x2mcp`, workflow file `publish.yml`) and a `NUGET_USER` secret holding the nuget.org profile name.
- 2-space indentation, matches `actions/*` action versions already pinned in these files — keep new steps consistent with the existing style rather than introducing a different formatting convention.
- Every job and every step must have a `name:` — no unnamed/anonymous steps or jobs. Names are Pascal Case (e.g. `Run Unit Tests`, `Verify Formatting`) and describe the action generically — never embed literal project paths, file names, CLI flags, or filter strings in a `name:` (those belong in the `run:` command itself).
- Adding a new target language's E2E test class (e.g. `PythonEndToEndTests`) should get its own `tests/integration/X2Mcp.Integration.<Language>.Tests` project and its own `integration-<language>` job in `build.yml`, installing only that language's toolchain, following the `X2Mcp.Integration.Go.Tests`/`integration-go` pattern.

## Project layout

- `src/X2Mcp.Core` — orchestration engine, abstractions, config, models.
- `src/X2Mcp.Cli` — CLI entry point (`x2mcp`), packaged as a .NET global tool.
- `src/X2Mcp.Language.{DotNet,Go,Python,Rust,Ruby}` — per-language scanner + wrapper emitter modules.
- `tests/unit/*.Tests` — unit tests per project.
- `tests/integration/X2Mcp.Integration.{DotNet,Go}.Tests` — one E2E test project per target language (scan → emit → build), each referencing only its own language module project, gated by the `Integration` trait.
