import { describe, it, expect } from "vitest";
import * as path from "node:path";
import * as url from "node:url";
import { extractPython } from "../../src/adapters/python/extractor.js";

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const FIXTURE = path.join(__dirname, "../fixtures/python/math.py");

describe("Python extractor", () => {
  it("extracts public functions", async () => {
    const tools = await extractPython(FIXTURE);
    const names = tools.map((t) => t.name);
    expect(names).toContain("add");
    expect(names).toContain("greet");
  });

  it("does not extract private functions", async () => {
    const tools = await extractPython(FIXTURE);
    expect(tools.map((t) => t.name)).not.toContain("_private_helper");
  });

  it("derives integer schema for int annotations", async () => {
    const tools = await extractPython(FIXTURE);
    const add = tools.find((t) => t.name === "add")!;
    expect(add).toBeDefined();
    expect(add.inputSchema.properties?.a).toEqual({ type: "integer" });
    expect(add.inputSchema.required).toContain("a");
  });

  it("marks defaulted parameters as not required", async () => {
    const tools = await extractPython(FIXTURE);
    const greet = tools.find((t) => t.name === "greet")!;
    expect(greet).toBeDefined();
    expect(greet.inputSchema.required).not.toContain("loud");
  });

  it("extracts the docstring as description", async () => {
    const tools = await extractPython(FIXTURE);
    const add = tools.find((t) => t.name === "add")!;
    expect(add.description).toMatch(/add two integers/i);
  });
});
