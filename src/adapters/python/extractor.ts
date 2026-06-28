import { execFile } from "node:child_process";
import * as path from "node:path";
import * as url from "node:url";
import { promisify } from "node:util";
import type { ToolDefinition } from "../../core/types.js";

const execFileAsync = promisify(execFile);

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const HELPER_SCRIPT = path.resolve(__dirname, "../../../scripts/extract_python.py");

/**
 * Invokes the bundled Python helper script to extract tool definitions from a
 * Python source file.  Requires Python 3.8+ on PATH.
 */
export async function extractPython(filePath: string): Promise<ToolDefinition[]> {
  let stdout: string;
  try {
    const result = await execFileAsync("python3", [HELPER_SCRIPT, filePath], {
      timeout: 30_000,
      maxBuffer: 10 * 1024 * 1024,
    });
    stdout = result.stdout;
  } catch (err: unknown) {
    const execErr = err as { stderr?: string; message?: string };
    // Try python as fallback (Windows / some systems omit the "3" suffix)
    try {
      const result = await execFileAsync("python", [HELPER_SCRIPT, filePath], {
        timeout: 30_000,
        maxBuffer: 10 * 1024 * 1024,
      });
      stdout = result.stdout;
    } catch {
      throw new Error(
        `Python extractor failed: ${execErr.stderr ?? execErr.message ?? String(err)}`,
      );
    }
  }

  try {
    return JSON.parse(stdout) as ToolDefinition[];
  } catch {
    throw new Error(`Python extractor returned invalid JSON:\n${stdout}`);
  }
}
