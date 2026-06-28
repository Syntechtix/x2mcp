import { describe, it, expect } from "vitest";
import * as path from "node:path";
import * as url from "node:url";
import { extractTypeScript } from "../../src/adapters/typescript/extractor.js";

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const FIXTURE = path.join(__dirname, "../fixtures/typescript/math.ts");

describe("TypeScript extractor", () => {
  it("extracts exported function declarations", async () => {
    const tools = await extractTypeScript(FIXTURE);
    const names = tools.map((t) => t.name);
    expect(names).toContain("add");
    expect(names).toContain("greet");
  });

  it("does not extract non-exported functions", async () => {
    const tools = await extractTypeScript(FIXTURE);
    expect(tools.map((t) => t.name)).not.toContain("internal");
  });

  it("extracts arrow function exports", async () => {
    const tools = await extractTypeScript(FIXTURE);
    expect(tools.map((t) => t.name)).toContain("fahrenheitToCelsius");
  });

  it("derives correct JSON schema for numeric parameters", async () => {
    const tools = await extractTypeScript(FIXTURE);
    const add = tools.find((t) => t.name === "add")!;
    expect(add).toBeDefined();
    expect(add.inputSchema.properties?.a).toEqual({ type: "number" });
    expect(add.inputSchema.properties?.b).toEqual({ type: "number" });
    expect(add.inputSchema.required).toContain("a");
    expect(add.inputSchema.required).toContain("b");
  });

  it("marks optional parameters as not required", async () => {
    const tools = await extractTypeScript(FIXTURE);
    const greet = tools.find((t) => t.name === "greet")!;
    expect(greet).toBeDefined();
    expect(greet.inputSchema.required).not.toContain("loud");
  });

  it("extracts JSDoc description", async () => {
    const tools = await extractTypeScript(FIXTURE);
    const add = tools.find((t) => t.name === "add")!;
    expect(add.description).toMatch(/adds two numbers/i);
  });
});
