"""
mcpify Python extractor helper.

This script is invoked by the mcpify Node.js process via subprocess.
It parses a Python source file using the `ast` module and prints a
JSON array of tool definitions to stdout.

Usage: python3 extract_python.py <source_file>
"""

import ast
import json
import sys
from pathlib import Path


def type_annotation_to_schema(annotation) -> dict:
    """Convert a Python type annotation AST node to a JSON Schema dict."""
    if annotation is None:
        return {"type": "string"}

    if isinstance(annotation, ast.Constant):
        if annotation.value is None:
            return {"type": "null"}
        return {"type": "string"}

    if isinstance(annotation, ast.Name):
        name = annotation.id
        mapping = {
            "str": {"type": "string"},
            "int": {"type": "integer"},
            "float": {"type": "number"},
            "bool": {"type": "boolean"},
            "None": {"type": "null"},
            "list": {"type": "array", "items": {}},
            "dict": {"type": "object"},
            "Any": {},
        }
        return mapping.get(name, {"type": "string"})

    if isinstance(annotation, ast.Subscript):
        # Handle generic types: List[X], Dict[K, V], Optional[X], Union[X, Y]
        if isinstance(annotation.value, ast.Name):
            outer = annotation.value.id
            if outer in ("List", "list"):
                inner = type_annotation_to_schema(annotation.slice)
                return {"type": "array", "items": inner}
            if outer in ("Dict", "dict"):
                return {"type": "object"}
            if outer == "Optional":
                inner = type_annotation_to_schema(annotation.slice)
                return {"anyOf": [inner, {"type": "null"}]}
            if outer == "Union":
                if isinstance(annotation.slice, ast.Tuple):
                    members = [type_annotation_to_schema(e) for e in annotation.slice.elts]
                    return {"anyOf": members}
        return {}

    return {}


def extract_docstring(node) -> str:
    """Return the docstring of a function/class node, or empty string."""
    if (
        node.body
        and isinstance(node.body[0], ast.Expr)
        and isinstance(node.body[0].value, ast.Constant)
        and isinstance(node.body[0].value.value, str)
    ):
        return node.body[0].value.value.strip()
    return ""


def parse_param_docs(docstring: str) -> dict:
    """Very simple Google/NumPy-style param doc parser.

    Returns a dict mapping param name → description.
    """
    result = {}
    lines = docstring.splitlines()
    in_args = False
    current_param = None
    for line in lines:
        stripped = line.strip()
        if stripped.lower() in ("args:", "arguments:", "parameters:", "params:"):
            in_args = True
            current_param = None
            continue
        if in_args and stripped and not stripped.startswith(" ") and stripped.endswith(":"):
            # End of Args section
            in_args = False
            current_param = None
            continue
        if in_args:
            # param_name (type): description
            import re
            m = re.match(r"^\s+(\w+)(?:\s*\([^)]*\))?\s*:\s*(.+)", line)
            if m:
                current_param = m.group(1)
                result[current_param] = m.group(2).strip()
            elif current_param and stripped:
                result[current_param] = result.get(current_param, "") + " " + stripped
    return result


def extract_functions(source_file: str) -> list:
    """Return a list of tool dicts for all public top-level functions."""
    source = Path(source_file).read_text(encoding="utf-8")
    tree = ast.parse(source, filename=source_file)

    tools = []
    for node in ast.iter_child_nodes(tree):
        if not isinstance(node, ast.FunctionDef) and not isinstance(node, ast.AsyncFunctionDef):
            continue
        # Skip private / dunder functions
        if node.name.startswith("_"):
            continue

        docstring = extract_docstring(node)
        param_docs = parse_param_docs(docstring)

        parameters = []
        properties = {}
        required = []

        args = node.args
        # Collect all args with their annotations
        all_args = args.args.copy()
        # Compute defaults alignment
        defaults = args.defaults
        first_default_idx = len(all_args) - len(defaults)

        for idx, arg in enumerate(all_args):
            if arg.arg == "self" or arg.arg == "cls":
                continue
            schema = type_annotation_to_schema(arg.annotation)
            is_required = idx < first_default_idx
            param = {
                "name": arg.arg,
                "description": param_docs.get(arg.arg, ""),
                "schema": schema,
                "required": is_required,
            }
            parameters.append(param)
            properties[arg.arg] = schema
            if is_required:
                required.append(arg.arg)

        # Only include the first non-self sentence of the docstring as description
        description = docstring.split("\n\n")[0].replace("\n", " ").strip() if docstring else ""

        tools.append({
            "name": node.name,
            "description": description,
            "inputSchema": {
                "type": "object",
                "properties": properties,
                "required": required,
            },
            "parameters": parameters,
            "sourceFile": source_file,
            "sourceLine": node.lineno,
        })

    return tools


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(json.dumps({"error": "Usage: extract_python.py <source_file>"}), file=sys.stderr)
        sys.exit(1)

    result = extract_functions(sys.argv[1])
    print(json.dumps(result, indent=2))
