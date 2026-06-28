import { describe, it, expect, afterEach } from "vitest";
import * as fs from "node:fs";
import * as path from "node:path";
import * as os from "node:os";
import * as url from "node:url";
import { extractGo } from "../../src/adapters/go/extractor.js";
import { generateGo } from "../../src/adapters/go/generator.js";
import type { MCPServerSpec } from "../../src/core/types.js";

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const FIXTURE = path.join(__dirname, "../fixtures/go/math.go");

let tmpDir: string;

afterEach(() => {
  if (tmpDir && fs.existsSync(tmpDir)) {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  }
});

describe("Go generator", () => {
  it("generates a main.go file", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-go-gen-"));
    const tools = await extractGo(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "go",
      tools,
      moduleName: "math",
    };
    const serverFile = await generateGo(spec, tmpDir);
    expect(fs.existsSync(serverFile)).toBe(true);
    expect(path.basename(serverFile)).toBe("main.go");
  });

  it("generated file references tool names", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-go-gen-"));
    const tools = await extractGo(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "go",
      tools,
      moduleName: "math",
    };
    await generateGo(spec, tmpDir);
    const content = fs.readFileSync(path.join(tmpDir, "main.go"), "utf-8");
    expect(content).toContain('"Add"');
    expect(content).toContain('"Greet"');
  });

  it("generated file imports mcp-go", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-go-gen-"));
    const tools = await extractGo(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "go",
      tools,
      moduleName: "math",
    };
    await generateGo(spec, tmpDir);
    const content = fs.readFileSync(path.join(tmpDir, "main.go"), "utf-8");
    expect(content).toContain("mark3labs/mcp-go");
  });

  it("generates a go.mod file", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-go-gen-"));
    const tools = await extractGo(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "go",
      tools,
      moduleName: "math",
    };
    await generateGo(spec, tmpDir);
    expect(fs.existsSync(path.join(tmpDir, "go.mod"))).toBe(true);
  });
});
