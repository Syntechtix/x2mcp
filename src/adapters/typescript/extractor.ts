import * as fs from "node:fs";
import * as path from "node:path";
import type { JSONSchema, ParameterDefinition, ToolDefinition } from "../../core/types.js";

// We import TypeScript's compiler API dynamically so that the host package
// does not require TypeScript at runtime on the user's machine — it falls back
// gracefully when TypeScript is unavailable.

/**
 * Extracts exported functions from a TypeScript/JavaScript source file using
 * the TypeScript compiler API.
 */
export async function extractTypeScript(filePath: string): Promise<ToolDefinition[]> {
  let ts: typeof import("typescript");
  try {
    ts = (await import("typescript")).default as typeof import("typescript");
  } catch {
    throw new Error("TypeScript compiler API not found. Run: npm install typescript");
  }

  const source = fs.readFileSync(filePath, "utf-8");
  const sourceFile = ts.createSourceFile(
    filePath,
    source,
    ts.ScriptTarget.Latest,
    /* setParentNodes */ true,
  );

  const tools: ToolDefinition[] = [];

  function visit(node: import("typescript").Node): void {
    // Exported function declarations: export function foo(...) {}
    if (ts.isFunctionDeclaration(node) && node.name && hasExportModifier(ts, node)) {
      const tool = buildTool(ts, node, node.name.text, node, filePath, sourceFile, source);
      if (tool) tools.push(tool);
    }

    // Exported variable declarations: export const foo = (...) => {}
    if (ts.isVariableStatement(node) && hasExportModifier(ts, node)) {
      for (const decl of node.declarationList.declarations) {
        if (
          ts.isIdentifier(decl.name) &&
          decl.initializer &&
          (ts.isArrowFunction(decl.initializer) || ts.isFunctionExpression(decl.initializer))
        ) {
          // The JSDoc comment belongs to the VariableStatement, not the arrow
          // function itself, so pass `node` (the statement) as the doc node.
          const tool = buildTool(
            ts,
            decl.initializer,
            decl.name.text,
            node,
            filePath,
            sourceFile,
            source,
          );
          if (tool) tools.push(tool);
        }
      }
    }

    ts.forEachChild(node, visit);
  }

  visit(sourceFile);
  return tools;
}

type FunctionLike =
  | import("typescript").FunctionDeclaration
  | import("typescript").ArrowFunction
  | import("typescript").FunctionExpression;

function buildTool(
  ts: typeof import("typescript"),
  node: FunctionLike,
  name: string,
  docNode: import("typescript").Node,
  filePath: string,
  sourceFile: import("typescript").SourceFile,
  source: string,
): ToolDefinition | null {
  const parameters: ParameterDefinition[] = [];
  const required: string[] = [];
  const properties: Record<string, JSONSchema> = {};

  for (const param of node.parameters) {
    if (!ts.isIdentifier(param.name)) continue;
    const paramName = param.name.text;
    const isOptional = !!param.questionToken || !!param.initializer;
    const schema = typeNodeToSchema(ts, param.type);
    const paramDef: ParameterDefinition = {
      name: paramName,
      description: extractParamDoc(name, paramName, source),
      schema,
      required: !isOptional,
    };
    parameters.push(paramDef);
    properties[paramName] = schema;
    if (!isOptional) required.push(paramName);
  }

  const description = extractJsDoc(docNode, sourceFile, source);
  const { line } = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile));

  return {
    name,
    description,
    inputSchema: {
      type: "object",
      properties,
      required,
    },
    parameters,
    sourceFile: filePath,
    sourceLine: line + 1,
  };
}

function hasExportModifier(
  ts: typeof import("typescript"),
  node: import("typescript").Node,
): boolean {
  const modifiers = ts.canHaveModifiers(node) ? ts.getModifiers(node) : undefined;
  return modifiers?.some((m) => m.kind === ts.SyntaxKind.ExportKeyword) ?? false;
}

function typeNodeToSchema(
  ts: typeof import("typescript"),
  typeNode: import("typescript").TypeNode | undefined,
): JSONSchema {
  if (!typeNode) return { type: "string" };

  switch (typeNode.kind) {
    case ts.SyntaxKind.StringKeyword:
      return { type: "string" };
    case ts.SyntaxKind.NumberKeyword:
      return { type: "number" };
    case ts.SyntaxKind.BooleanKeyword:
      return { type: "boolean" };
    case ts.SyntaxKind.AnyKeyword:
    case ts.SyntaxKind.UnknownKeyword:
      return {};
    case ts.SyntaxKind.VoidKeyword:
    case ts.SyntaxKind.UndefinedKeyword:
      return { type: "null" };
    case ts.SyntaxKind.ArrayType: {
      const at = typeNode as import("typescript").ArrayTypeNode;
      return { type: "array", items: typeNodeToSchema(ts, at.elementType) };
    }
    case ts.SyntaxKind.TypeLiteral: {
      const tl = typeNode as import("typescript").TypeLiteralNode;
      const props: Record<string, JSONSchema> = {};
      const req: string[] = [];
      for (const member of tl.members) {
        if (ts.isPropertySignature(member) && member.name && ts.isIdentifier(member.name)) {
          props[member.name.text] = typeNodeToSchema(ts, member.type);
          if (!member.questionToken) req.push(member.name.text);
        }
      }
      return { type: "object", properties: props, required: req };
    }
    case ts.SyntaxKind.UnionType: {
      const ut = typeNode as import("typescript").UnionTypeNode;
      // Simple union of literals → enum
      const literals: unknown[] = [];
      for (const t of ut.types) {
        if (ts.isLiteralTypeNode(t)) {
          const lit = t.literal;
          if (ts.isStringLiteral(lit)) literals.push(lit.text);
          else if (ts.isNumericLiteral(lit)) literals.push(Number(lit.text));
        }
      }
      if (literals.length === ut.types.length) {
        return { enum: literals };
      }
      return {};
    }
    default:
      return { type: "string" };
  }
}

/** Extract the leading JSDoc/TSDoc comment for a node.
 *
 * JSDoc comments are part of the node's leading trivia — they live in the
 * range [getFullStart(), getStart()].  We search that slice for the LAST
 * /** ... *\/ block, which is the one directly attached to the node (earlier
 * blocks may be file-level or other declarations' trailing trivia).
 */
function extractJsDoc(
  node: import("typescript").Node,
  sourceFile: import("typescript").SourceFile,
  source: string,
): string {
  const fullStart = node.getFullStart();
  const start = node.getStart(sourceFile, /* includeJsDocComment */ false);
  const trivia = source.slice(fullStart, start);

  // Collect ALL /** ... */ blocks and take the last one
  const allMatches = [...trivia.matchAll(/\/\*\*([\s\S]*?)\*\//g)];
  if (allMatches.length > 0) {
    const last = allMatches[allMatches.length - 1];
    return last[1]
      .split("\n")
      .map((l) => l.replace(/^\s*\*\s?/, "").trim())
      .filter(Boolean)
      .filter((l) => !l.startsWith("@"))
      .join(" ");
  }

  // Fall back to single-line comment immediately preceding the node
  const lineMatch = trivia.match(/\/\/\s*(.+)\s*[\r\n]+\s*$/);
  return lineMatch ? lineMatch[1].trim() : "";
}

/** Extract @param description for a specific parameter from JSDoc. */
function extractParamDoc(_funcName: string, paramName: string, source: string): string {
  const re = new RegExp(`@param\\s+\\{?[^}]*\\}?\\s+${paramName}\\s+-?\\s*(.+)`);
  const match = source.match(re);
  return match ? match[1].trim() : "";
}

/** Resolve the canonical module specifier to import `filePath` from `outputDir`. */
export function relativeImport(outputDir: string, filePath: string): string {
  let rel = path.relative(outputDir, filePath).replace(/\\/g, "/");
  if (!rel.startsWith(".")) rel = "./" + rel;
  // Strip .ts extension for the generated import (consumers use .js at runtime with ts-node / tsx)
  return rel.replace(/\.ts$/, ".js");
}
