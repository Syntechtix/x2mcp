#!/usr/bin/env node
/**
 * mcpify CLI entry point.
 *
 * Commands:
 *   mcpify generate <path>  — extract tools and generate an MCP server
 *   mcpify run <path>       — generate + start the MCP server (stdio)
 *   mcpify init <path>      — same as generate, with extra onboarding output
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { spawn } from "node:child_process";
import { Command } from "commander";
import { Orchestrator } from "./core/orchestrator.js";
import { TypeScriptAdapter } from "./adapters/typescript/index.js";
import { PythonAdapter } from "./adapters/python/index.js";
import { GoAdapter } from "./adapters/go/index.js";
import type { McpifyConfig, SupportedLanguage } from "./core/types.js";

const program = new Command();

program
  .name("mcpify")
  .description("Create a native-language MCP server wrapper around any codebase")
  .version("0.1.0");

// ─── Shared option builder ────────────────────────────────────────────────────

function addSharedOptions(cmd: Command): Command {
  return cmd
    .argument("<path>", "Source file or directory to wrap")
    .option("-l, --language <lang>", "Force language (typescript | python | go)")
    .option("-o, --output <dir>", "Output directory for the generated server")
    .option("-c, --config <file>", "Path to mcpify.config.json");
}

// ─── generate ─────────────────────────────────────────────────────────────────

addSharedOptions(
  program
    .command("generate")
    .alias("gen")
    .description("Generate an MCP server from source code"),
).action(async (inputPath: string, opts: SharedOpts) => {
  const { orchestrator, config } = setup(opts);
  try {
    const out = await orchestrator.run(inputPath, {
      language: opts.language as SupportedLanguage | undefined,
      outputDir: opts.output,
      config,
    });
    console.log(`✅  MCP server generated: ${out}`);
    printRunInstructions(out);
  } catch (err) {
    console.error(`❌  ${(err as Error).message}`);
    process.exit(1);
  }
});

// ─── init (alias for generate with friendlier output) ─────────────────────────

addSharedOptions(
  program
    .command("init")
    .description("Scaffold an MCP server from source code (same as generate)"),
).action(async (inputPath: string, opts: SharedOpts) => {
  console.log(`🔍  Analysing ${inputPath} …`);
  const { orchestrator, config } = setup(opts);
  try {
    const out = await orchestrator.run(inputPath, {
      language: opts.language as SupportedLanguage | undefined,
      outputDir: opts.output,
      config,
    });
    console.log(`\n✅  MCP server initialised at: ${out}`);
    printRunInstructions(out);
    printMCPClientHint(out);
  } catch (err) {
    console.error(`\n❌  ${(err as Error).message}`);
    process.exit(1);
  }
});

// ─── run ──────────────────────────────────────────────────────────────────────

addSharedOptions(
  program
    .command("run")
    .description("Generate and start the MCP server on stdio"),
).action(async (inputPath: string, opts: SharedOpts) => {
  console.error(`🔍  Generating MCP server for ${inputPath} …`);
  const { orchestrator, config } = setup(opts);
  let entryPoint: string;
  try {
    entryPoint = await orchestrator.run(inputPath, {
      language: opts.language as SupportedLanguage | undefined,
      outputDir: opts.output,
      config,
    });
  } catch (err) {
    console.error(`❌  ${(err as Error).message}`);
    process.exit(1);
    return;
  }

  console.error(`🚀  Starting: ${entryPoint}`);
  const runner = resolveRunner(entryPoint);
  const child = spawn(runner.cmd, [...runner.args, entryPoint], {
    stdio: "inherit",
    env: process.env,
  });
  child.on("exit", (code) => process.exit(code ?? 0));
  child.on("error", (err) => {
    console.error(`Failed to start server: ${err.message}`);
    process.exit(1);
  });
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

interface SharedOpts {
  language?: string;
  output?: string;
  config?: string;
}

function setup(opts: SharedOpts): { orchestrator: Orchestrator; config: McpifyConfig } {
  const orchestrator = new Orchestrator()
    .register(new TypeScriptAdapter())
    .register(new PythonAdapter())
    .register(new GoAdapter());

  let config: McpifyConfig = {};
  const configPath = opts.config ?? findConfig();
  if (configPath && fs.existsSync(configPath)) {
    try {
      config = JSON.parse(fs.readFileSync(configPath, "utf-8")) as McpifyConfig;
    } catch (err) {
      console.warn(`⚠️  Could not parse config file "${configPath}": ${(err as Error).message}`);
    }
  }
  return { orchestrator, config };
}

/** Walk up from cwd looking for mcpify.config.json. */
function findConfig(): string | null {
  let dir = process.cwd();
  while (true) {
    const candidate = path.join(dir, "mcpify.config.json");
    if (fs.existsSync(candidate)) return candidate;
    const parent = path.dirname(dir);
    if (parent === dir) return null;
    dir = parent;
  }
}

function resolveRunner(entryPoint: string): { cmd: string; args: string[] } {
  const ext = path.extname(entryPoint);
  switch (ext) {
    case ".ts":
      return { cmd: "tsx", args: [] };
    case ".js":
      return { cmd: "node", args: [] };
    case ".py":
      return { cmd: "python3", args: [] };
    default:
      // Go: assume it has been compiled
      return { cmd: entryPoint, args: [] };
  }
}

function printRunInstructions(entryPoint: string): void {
  const ext = path.extname(entryPoint);
  if (ext === ".ts" || ext === ".js") {
    console.log(`\n  cd ${path.dirname(entryPoint)}`);
    console.log("  npm install");
    console.log(`  npx tsx ${path.basename(entryPoint)}\n`);
  } else if (ext === ".py") {
    console.log(`\n  cd ${path.dirname(entryPoint)}`);
    console.log("  pip install -r requirements.txt");
    console.log(`  python3 ${path.basename(entryPoint)}\n`);
  } else {
    console.log(`\n  cd ${path.dirname(entryPoint)}`);
    console.log("  go mod tidy");
    console.log("  go run .\n");
  }
}

function printMCPClientHint(entryPoint: string): void {
  const abs = path.resolve(entryPoint);
  const ext = path.extname(entryPoint);
  let cmd: string;
  if (ext === ".ts") cmd = `npx tsx ${abs}`;
  else if (ext === ".js") cmd = `node ${abs}`;
  else if (ext === ".py") cmd = `python3 ${abs}`;
  else cmd = abs;

  console.log("Add to your MCP client config (e.g. Claude Desktop):");
  console.log(
    JSON.stringify(
      {
        mcpServers: {
          [path.basename(path.dirname(abs))]: { command: cmd },
        },
      },
      null,
      2,
    ),
  );
}

program.parse();
