import { describe, it, expect } from "vitest";
import * as path from "node:path";
import * as url from "node:url";
import { extractGo } from "../../src/adapters/go/extractor.js";

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const FIXTURE = path.join(__dirname, "../fixtures/go/math.go");

describe("Go extractor", () => {
  it("extracts exported functions", async () => {
    const tools = await extractGo(FIXTURE);
    const names = tools.map((t) => t.name);
    expect(names).toContain("Add");
    expect(names).toContain("Greet");
  });

  it("does not extract unexported functions", async () => {
    const tools = await extractGo(FIXTURE);
    expect(tools.map((t) => t.name)).not.toContain("internal");
  });

  it("derives integer schema for int parameters", async () => {
    const tools = await extractGo(FIXTURE);
    const add = tools.find((t) => t.name === "Add")!;
    expect(add).toBeDefined();
    expect(add.inputSchema.properties?.a).toEqual({ type: "integer" });
    expect(add.inputSchema.required).toContain("a");
  });

  it("extracts the godoc comment as description", async () => {
    const tools = await extractGo(FIXTURE);
    const add = tools.find((t) => t.name === "Add")!;
    expect(add.description).toMatch(/sum of a and b/i);
  });
});
