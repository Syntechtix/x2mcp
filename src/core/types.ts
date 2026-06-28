/**
 * Core shared types for mcpify.
 *
 * These types form the intermediate representation (IR) that flows between
 * language-specific extractors and generators.
 */

/** A JSON Schema object (subset used by MCP tool input schemas). */
export interface JSONSchema {
  type?: string | string[];
  properties?: Record<string, JSONSchema>;
  items?: JSONSchema;
  required?: string[];
  description?: string;
  enum?: unknown[];
  default?: unknown;
  $ref?: string;
  [key: string]: unknown;
}

/** A single parameter of a tool function. */
export interface ParameterDefinition {
  name: string;
  description: string;
  schema: JSONSchema;
  required: boolean;
}

/** Describes a single callable tool extracted from source code. */
export interface ToolDefinition {
  /** Identifier used in MCP tools/call requests. */
  name: string;
  /** Human-readable description, sourced from doc comments when available. */
  description: string;
  /** JSON Schema describing the tool's input object. */
  inputSchema: JSONSchema;
  /** Ordered list of parameters for use in generated call sites. */
  parameters: ParameterDefinition[];
  /**
   * The original source location (file + line) for debugging and traceability.
   */
  sourceFile: string;
  sourceLine?: number;
}

/**
 * The complete specification passed from extractor → generator.
 * Carries all tools extracted from a project plus metadata about the origin.
 */
export interface MCPServerSpec {
  /** Absolute path to the source file or directory that was analysed. */
  sourcePath: string;
  /** Detected or override language. */
  language: SupportedLanguage;
  /** All tools discovered in the source. */
  tools: ToolDefinition[];
  /** The package/module name inferred from the source. */
  moduleName: string;
}

/** Languages that mcpify can generate MCP servers for. */
export type SupportedLanguage = "typescript" | "python" | "go";

/**
 * Configuration loaded from `mcpify.config.json`.
 */
export interface McpifyConfig {
  /** Glob patterns (relative to config file) for source files to include. */
  include?: string[];
  /** Glob patterns for source files to exclude. */
  exclude?: string[];
  /** Override descriptions for specific tools by name. */
  toolDescriptions?: Record<string, string>;
  /** Directory (relative to config file) where the generated server is written. */
  outputDir?: string;
  /** Force a specific language instead of auto-detecting. */
  language?: SupportedLanguage;
  /** Server name embedded in the generated MCP server metadata. */
  serverName?: string;
  /** Server version embedded in the generated MCP server metadata. */
  serverVersion?: string;
}

/**
 * Contract that every language adapter must implement.
 *
 * Adapters are registered with the Orchestrator and selected automatically
 * based on `detect()` or the user's `--language` override.
 */
export interface LanguageAdapter {
  /** Human-readable adapter name (e.g. "TypeScript"). */
  readonly name: string;

  /**
   * Returns true when this adapter should handle `filePath`.
   * Called during language auto-detection.
   */
  detect(filePath: string): boolean;

  /**
   * Parses `filePath` and returns all callable tools found there.
   */
  extract(filePath: string): Promise<ToolDefinition[]>;

  /**
   * Writes a ready-to-run MCP server into `outputDir` based on `spec`.
   * Returns the path of the generated entry-point file.
   */
  generate(spec: MCPServerSpec, outputDir: string): Promise<string>;
}
