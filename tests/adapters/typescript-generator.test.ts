import { describe, it, expect, afterEach } from "vitest";
import * as fs from "node:fs";
import * as path from "node:path";
import * as os from "node:os";
import * as url from "node:url";
import { extractTypeScript } from "../../src/adapters/typescript/extractor.js";
import { generateTypeScript } from "../../src/adapters/typescript/generator.js";
import type { MCPServerSpec } from "../../src/core/types.js";

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const FIXTURE = path.join(__dirname, "../fixtures/typescript/math.ts");

let tmpDir: string;

afterEach(() => {
  if (tmpDir && fs.existsSync(tmpDir)) {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  }
});

describe("TypeScript generator", () => {
  it("generates a server.ts file", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-ts-"));
    const tools = await extractTypeScript(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "typescript",
      tools,
      moduleName: "math",
    };
    const serverFile = await generateTypeScript(spec, tmpDir);
    expect(fs.existsSync(serverFile)).toBe(true);
    expect(path.basename(serverFile)).toBe("server.ts");
  });

  it("generated file references tool names", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-ts-"));
    const tools = await extractTypeScript(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "typescript",
      tools,
      moduleName: "math",
    };
    await generateTypeScript(spec, tmpDir);
    const content = fs.readFileSync(path.join(tmpDir, "server.ts"), "utf-8");
    expect(content).toContain('"add"');
    expect(content).toContain('"greet"');
    expect(content).toContain('"fahrenheitToCelsius"');
  });

  it("generated file contains McpServer import", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-ts-"));
    const tools = await extractTypeScript(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "typescript",
      tools,
      moduleName: "math",
    };
    await generateTypeScript(spec, tmpDir);
    const content = fs.readFileSync(path.join(tmpDir, "server.ts"), "utf-8");
    expect(content).toContain("@modelcontextprotocol/sdk");
    expect(content).toContain("McpServer");
  });

  it("generates a package.json alongside server.ts", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-ts-"));
    const tools = await extractTypeScript(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "typescript",
      tools,
      moduleName: "math",
    };
    await generateTypeScript(spec, tmpDir);
    expect(fs.existsSync(path.join(tmpDir, "package.json"))).toBe(true);
  });
});
