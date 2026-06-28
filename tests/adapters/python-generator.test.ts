import { describe, it, expect, afterEach } from "vitest";
import * as fs from "node:fs";
import * as path from "node:path";
import * as os from "node:os";
import * as url from "node:url";
import { extractPython } from "../../src/adapters/python/extractor.js";
import { generatePython } from "../../src/adapters/python/generator.js";
import type { MCPServerSpec } from "../../src/core/types.js";

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const FIXTURE = path.join(__dirname, "../fixtures/python/math.py");

let tmpDir: string;

afterEach(() => {
  if (tmpDir && fs.existsSync(tmpDir)) {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  }
});

describe("Python generator", () => {
  it("generates a server.py file", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-py-"));
    const tools = await extractPython(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "python",
      tools,
      moduleName: "math",
    };
    const serverFile = await generatePython(spec, tmpDir);
    expect(fs.existsSync(serverFile)).toBe(true);
    expect(path.basename(serverFile)).toBe("server.py");
  });

  it("generated file references tool names", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-py-"));
    const tools = await extractPython(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "python",
      tools,
      moduleName: "math",
    };
    await generatePython(spec, tmpDir);
    const content = fs.readFileSync(path.join(tmpDir, "server.py"), "utf-8");
    expect(content).toContain('"add"');
    expect(content).toContain('"greet"');
  });

  it("generated file imports mcp", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-py-"));
    const tools = await extractPython(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "python",
      tools,
      moduleName: "math",
    };
    await generatePython(spec, tmpDir);
    const content = fs.readFileSync(path.join(tmpDir, "server.py"), "utf-8");
    expect(content).toContain("from mcp");
  });

  it("generates a requirements.txt", async () => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "mcpify-py-"));
    const tools = await extractPython(FIXTURE);
    const spec: MCPServerSpec = {
      sourcePath: FIXTURE,
      language: "python",
      tools,
      moduleName: "math",
    };
    await generatePython(spec, tmpDir);
    expect(fs.existsSync(path.join(tmpDir, "requirements.txt"))).toBe(true);
  });
});
