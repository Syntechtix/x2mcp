import { describe, it, expect } from "vitest";
import * as path from "node:path";
import * as url from "node:url";
import { detectLanguage } from "../../src/core/detector.js";

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const FIXTURES = path.join(__dirname, "../fixtures");

describe("detectLanguage", () => {
  it("detects typescript for .ts files", () => {
    expect(detectLanguage(path.join(FIXTURES, "typescript/math.ts"))).toBe("typescript");
  });

  it("detects python for .py files", () => {
    expect(detectLanguage(path.join(FIXTURES, "python/math.py"))).toBe("python");
  });

  it("detects go for .go files", () => {
    expect(detectLanguage(path.join(FIXTURES, "go/math.go"))).toBe("go");
  });

  it("returns null for unknown extensions", () => {
    expect(detectLanguage("/some/file.unknown")).toBeNull();
  });

  it("detects the majority language in a directory", () => {
    // typescript/ has only .ts files
    expect(detectLanguage(path.join(FIXTURES, "typescript"))).toBe("typescript");
  });
});
