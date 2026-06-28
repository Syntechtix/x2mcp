import * as fs from "node:fs";
import * as path from "node:path";
import type { SupportedLanguage } from "./types.js";

/** Maps file extensions to their language. */
const EXTENSION_MAP: Record<string, SupportedLanguage> = {
  ".ts": "typescript",
  ".tsx": "typescript",
  ".js": "typescript",
  ".mjs": "typescript",
  ".cjs": "typescript",
  ".py": "python",
  ".go": "go",
};

/**
 * Attempts to detect the primary language of the given file or directory.
 *
 * For a directory, it looks at the first source file it finds.
 * Returns `null` when the language cannot be determined.
 */
export function detectLanguage(inputPath: string): SupportedLanguage | null {
  const stat = fs.statSync(inputPath, { throwIfNoEntry: false });
  if (!stat) return null;

  if (stat.isFile()) {
    return fromExtension(inputPath);
  }

  // Directory: count files per language and pick the majority.
  const counts: Partial<Record<SupportedLanguage, number>> = {};
  walkDir(inputPath, (filePath) => {
    const lang = fromExtension(filePath);
    if (lang) counts[lang] = (counts[lang] ?? 0) + 1;
  });

  let best: SupportedLanguage | null = null;
  let bestCount = 0;
  for (const [lang, count] of Object.entries(counts) as [SupportedLanguage, number][]) {
    if (count > bestCount) {
      bestCount = count;
      best = lang;
    }
  }
  return best;
}

/** Returns the language for a file by its extension, or null. */
export function fromExtension(filePath: string): SupportedLanguage | null {
  const ext = path.extname(filePath).toLowerCase();
  return EXTENSION_MAP[ext] ?? null;
}

/** Recursively walk a directory, calling `cb` for every regular file. */
function walkDir(dir: string, cb: (filePath: string) => void): void {
  let entries: fs.Dirent[];
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.name.startsWith(".") || entry.name === "node_modules" || entry.name === "dist") {
      continue;
    }
    if (entry.isDirectory()) {
      walkDir(full, cb);
    } else if (entry.isFile()) {
      cb(full);
    }
  }
}

/** Collect all source files of the given language under a directory. */
export function collectSourceFiles(dir: string, language: SupportedLanguage): string[] {
  const files: string[] = [];
  const validExts = Object.entries(EXTENSION_MAP)
    .filter(([, lang]) => lang === language)
    .map(([ext]) => ext);

  walkDir(dir, (filePath) => {
    const ext = path.extname(filePath).toLowerCase();
    if (validExts.includes(ext)) {
      files.push(filePath);
    }
  });
  return files;
}
