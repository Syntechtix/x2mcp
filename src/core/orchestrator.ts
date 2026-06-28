import * as fs from "node:fs";
import * as path from "node:path";
import { detectLanguage, collectSourceFiles } from "./detector.js";
import type {
  LanguageAdapter,
  MCPServerSpec,
  McpifyConfig,
  SupportedLanguage,
  ToolDefinition,
} from "./types.js";

export interface OrchestratorOptions {
  /** Explicit language override — skips auto-detection. */
  language?: SupportedLanguage;
  /** Directory where the generated server will be written. */
  outputDir?: string;
  /** Config loaded from mcpify.config.json (if present). */
  config?: McpifyConfig;
}

/**
 * The Orchestrator wires language adapters together.
 *
 * Usage:
 *   const orch = new Orchestrator();
 *   orch.register(new TypeScriptAdapter());
 *   const outputFile = await orch.run("./src/math.ts", { outputDir: "./mcp-server" });
 */
export class Orchestrator {
  private adapters: LanguageAdapter[] = [];

  /** Register a language adapter. Later registrations take priority. */
  register(adapter: LanguageAdapter): this {
    this.adapters.push(adapter);
    return this;
  }

  /**
   * Detect language, extract tools, apply config overrides, then generate the
   * MCP server.  Returns the path to the generated entry-point file.
   */
  async run(inputPath: string, options: OrchestratorOptions = {}): Promise<string> {
    const absInput = path.resolve(inputPath);
    const config = options.config ?? {};

    // 1. Determine language
    const language =
      options.language ??
      config.language ??
      (() => {
        const detected = detectLanguage(absInput);
        if (!detected) {
          throw new Error(
            `Cannot detect language for "${absInput}". Use --language to specify one.`,
          );
        }
        return detected;
      })();

    // 2. Find adapter
    const adapter = this.adapters.find((a) => a.name.toLowerCase() === language);
    if (!adapter) {
      throw new Error(
        `No adapter registered for language "${language}". Available: ${this.adapters.map((a) => a.name).join(", ")}`,
      );
    }

    // 3. Collect source files
    const stat = fs.statSync(absInput);
    const sourceFiles = stat.isDirectory()
      ? collectSourceFiles(absInput, language)
      : [absInput];

    if (sourceFiles.length === 0) {
      throw new Error(`No ${language} source files found in "${absInput}".`);
    }

    // 4. Extract tools from each file
    const allTools: ToolDefinition[] = [];
    for (const file of sourceFiles) {
      const tools = await adapter.extract(file);
      allTools.push(...tools);
    }

    if (allTools.length === 0) {
      throw new Error(
        `No exported functions found in "${absInput}". Make sure you are pointing at a file with exported functions.`,
      );
    }

    // 5. Apply config overrides for descriptions
    const toolDescOverrides = config.toolDescriptions ?? {};
    const tools = allTools.map((t) => ({
      ...t,
      description: toolDescOverrides[t.name] ?? t.description,
    }));

    // 6. Determine output directory
    const outputDir = path.resolve(
      options.outputDir ?? config.outputDir ?? path.join(path.dirname(absInput), "mcp-server"),
    );
    fs.mkdirSync(outputDir, { recursive: true });

    // 7. Build spec
    const spec: MCPServerSpec = {
      sourcePath: absInput,
      language,
      tools,
      moduleName: config.serverName ?? inferModuleName(absInput),
    };

    // 8. Generate
    const entryPoint = await adapter.generate(spec, outputDir);
    return entryPoint;
  }
}

/** Infer a human-friendly module name from a file or directory path. */
function inferModuleName(inputPath: string): string {
  const base = path.basename(inputPath, path.extname(inputPath));
  return base === "index" || base === "main"
    ? path.basename(path.dirname(inputPath))
    : base;
}
