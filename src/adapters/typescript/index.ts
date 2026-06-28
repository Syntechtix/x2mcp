import type { LanguageAdapter, MCPServerSpec, ToolDefinition } from "../../core/types.js";
import { extractTypeScript } from "./extractor.js";
import { generateTypeScript } from "./generator.js";

export class TypeScriptAdapter implements LanguageAdapter {
  readonly name = "typescript";

  detect(filePath: string): boolean {
    return /\.(ts|tsx|js|mjs|cjs)$/.test(filePath);
  }

  async extract(filePath: string): Promise<ToolDefinition[]> {
    return extractTypeScript(filePath);
  }

  async generate(spec: MCPServerSpec, outputDir: string): Promise<string> {
    return generateTypeScript(spec, outputDir);
  }
}
