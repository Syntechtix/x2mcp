import type { LanguageAdapter, MCPServerSpec, ToolDefinition } from "../../core/types.js";
import { extractPython } from "./extractor.js";
import { generatePython } from "./generator.js";

export class PythonAdapter implements LanguageAdapter {
  readonly name = "python";

  detect(filePath: string): boolean {
    return /\.py$/.test(filePath);
  }

  async extract(filePath: string): Promise<ToolDefinition[]> {
    return extractPython(filePath);
  }

  async generate(spec: MCPServerSpec, outputDir: string): Promise<string> {
    return generatePython(spec, outputDir);
  }
}
