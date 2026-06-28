import { execFile, spawn } from "node:child_process";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import * as url from "node:url";
import { promisify } from "node:util";
import type { ToolDefinition } from "../../core/types.js";

const execFileAsync = promisify(execFile);

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const GO_SCRIPT = path.resolve(__dirname, "../../../scripts/extract_go.go");

/** Cache compiled binary path per process. */
let _compiledBin: string | null = null;

/**
 * Compiles the Go extractor helper (once) and then invokes it on `filePath`.
 * Requires `go` on PATH.
 */
export async function extractGo(filePath: string): Promise<ToolDefinition[]> {
  const binPath = await getCompiledBin();

  const { stdout } = await execFileAsync(binPath, [filePath], {
    timeout: 30_000,
    maxBuffer: 10 * 1024 * 1024,
  });

  try {
    return JSON.parse(stdout) as ToolDefinition[];
  } catch {
    throw new Error(`Go extractor returned invalid JSON:\n${stdout}`);
  }
}

async function getCompiledBin(): Promise<string> {
  if (_compiledBin && fs.existsSync(_compiledBin)) return _compiledBin;

  const tmpDir = path.join(os.tmpdir(), "mcpify-go-extractor");
  fs.mkdirSync(tmpDir, { recursive: true });
  const binName = process.platform === "win32" ? "extract_go.exe" : "extract_go";
  const binPath = path.join(tmpDir, binName);

  await new Promise<void>((resolve, reject) => {
    const child = spawn("go", ["build", "-o", binPath, GO_SCRIPT], { stdio: "pipe" });
    let stderr = "";
    child.stderr?.on("data", (d: Buffer) => (stderr += d.toString()));
    child.on("close", (code) => {
      if (code !== 0) {
        reject(new Error(`go build failed (exit ${code}): ${stderr}`));
      } else {
        resolve();
      }
    });
    child.on("error", (err) => {
      reject(new Error(`Could not spawn "go": ${err.message}. Is Go installed?`));
    });
  });

  _compiledBin = binPath;
  return binPath;
}
