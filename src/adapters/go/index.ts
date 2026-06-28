import type { LanguageAdapter, MCPServerSpec, ToolDefinition } from "../../core/types.js";
import { extractGo } from "./extractor.js";
import { generateGo } from "./generator.js";

export class GoAdapter implements LanguageAdapter {
  readonly name = "go";

  detect(filePath: string): boolean {
    return /\.go$/.test(filePath);
  }

  async extract(filePath: string): Promise<ToolDefinition[]> {
    return extractGo(filePath);
  }

  async generate(spec: MCPServerSpec, outputDir: string): Promise<string> {
    return generateGo(spec, outputDir);
  }
}
