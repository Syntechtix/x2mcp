/**
 * mcpify public API.
 *
 * Re-exports the core types and the Orchestrator so that mcpify can be used
 * as a library, not just a CLI tool.
 *
 * @example
 * ```ts
 * import { Orchestrator, TypeScriptAdapter } from "mcpify";
 *
 * const orch = new Orchestrator().register(new TypeScriptAdapter());
 * const serverFile = await orch.run("./src/math.ts", { outputDir: "./mcp-server" });
 * console.log("Generated:", serverFile);
 * ```
 */
export * from "./core/types.js";
export { Orchestrator } from "./core/orchestrator.js";
export { detectLanguage, collectSourceFiles } from "./core/detector.js";
export { TypeScriptAdapter } from "./adapters/typescript/index.js";
export { PythonAdapter } from "./adapters/python/index.js";
export { GoAdapter } from "./adapters/go/index.js";
