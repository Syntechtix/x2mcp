package main

import (
	"encoding/json"
	"fmt"
	"go/ast"
	"go/doc"
	"go/parser"
	"go/token"
	"os"
	"strings"
)

// JSONSchema is a minimal JSON Schema representation.
type JSONSchema struct {
	Type       string                 `json:"type,omitempty"`
	Properties map[string]*JSONSchema `json:"properties,omitempty"`
	Required   []string               `json:"required,omitempty"`
	Items      *JSONSchema            `json:"items,omitempty"`
}

// ParameterDef mirrors the TypeScript ParameterDefinition.
type ParameterDef struct {
	Name        string     `json:"name"`
	Description string     `json:"description"`
	Schema      JSONSchema `json:"schema"`
	Required    bool       `json:"required"`
}

// ToolDef mirrors the TypeScript ToolDefinition.
type ToolDef struct {
	Name        string         `json:"name"`
	Description string         `json:"description"`
	InputSchema JSONSchema     `json:"inputSchema"`
	Parameters  []ParameterDef `json:"parameters"`
	SourceFile  string         `json:"sourceFile"`
	SourceLine  int            `json:"sourceLine"`
}

func goTypeToSchema(expr ast.Expr) JSONSchema {
	switch t := expr.(type) {
	case *ast.Ident:
		switch t.Name {
		case "string":
			return JSONSchema{Type: "string"}
		case "int", "int8", "int16", "int32", "int64",
			"uint", "uint8", "uint16", "uint32", "uint64":
			return JSONSchema{Type: "integer"}
		case "float32", "float64":
			return JSONSchema{Type: "number"}
		case "bool":
			return JSONSchema{Type: "boolean"}
		default:
			return JSONSchema{Type: "object"}
		}
	case *ast.ArrayType:
		inner := goTypeToSchema(t.Elt)
		return JSONSchema{Type: "array", Items: &inner}
	case *ast.MapType:
		return JSONSchema{Type: "object"}
	case *ast.StarExpr:
		return goTypeToSchema(t.X)
	case *ast.SelectorExpr:
		return JSONSchema{Type: "object"}
	default:
		return JSONSchema{Type: "string"}
	}
}

func extractFunctions(sourceFile string) ([]ToolDef, error) {
	fset := token.NewFileSet()
	f, err := parser.ParseFile(fset, sourceFile, nil, parser.ParseComments)
	if err != nil {
		return nil, fmt.Errorf("parse error: %w", err)
	}

	// Build doc package for comment lookup
	pkg := &ast.Package{
		Name:  f.Name.Name,
		Files: map[string]*ast.File{sourceFile: f},
	}
	docPkg, err := doc.NewFromFiles(fset, pkg.Files, "")
	if err != nil {
		return nil, fmt.Errorf("doc error: %w", err)
	}

	// Index godoc comments by function name
	docMap := make(map[string]string)
	for _, fn := range docPkg.Funcs {
		docMap[fn.Name] = strings.TrimSpace(fn.Doc)
	}

	var tools []ToolDef
	for _, decl := range f.Decls {
		fd, ok := decl.(*ast.FuncDecl)
		if !ok {
			continue
		}
		// Only exported top-level functions (no receiver)
		if fd.Recv != nil || !ast.IsExported(fd.Name.Name) {
			continue
		}

		description := docMap[fd.Name.Name]
		pos := fset.Position(fd.Pos())

		var params []ParameterDef
		props := make(map[string]*JSONSchema)
		var required []string

		if fd.Type.Params != nil {
			for _, field := range fd.Type.Params.List {
				schema := goTypeToSchema(field.Type)
				for _, name := range field.Names {
					p := ParameterDef{
						Name:        name.Name,
						Description: "",
						Schema:      schema,
						Required:    true,
					}
					params = append(params, p)
					schemaCopy := schema
					props[name.Name] = &schemaCopy
					required = append(required, name.Name)
				}
			}
		}

		inputSchema := JSONSchema{
			Type:       "object",
			Properties: props,
			Required:   required,
		}
		if params == nil {
			params = []ParameterDef{}
		}

		tools = append(tools, ToolDef{
			Name:        fd.Name.Name,
			Description: description,
			InputSchema: inputSchema,
			Parameters:  params,
			SourceFile:  sourceFile,
			SourceLine:  pos.Line,
		})
	}
	if tools == nil {
		tools = []ToolDef{}
	}
	return tools, nil
}

func main() {
	if len(os.Args) != 2 {
		fmt.Fprintln(os.Stderr, "Usage: extract_go <source_file>")
		os.Exit(1)
	}
	tools, err := extractFunctions(os.Args[1])
	if err != nil {
		fmt.Fprintln(os.Stderr, "Error:", err)
		os.Exit(1)
	}
	enc := json.NewEncoder(os.Stdout)
	enc.SetIndent("", "  ")
	if err := enc.Encode(tools); err != nil {
		fmt.Fprintln(os.Stderr, "JSON encode error:", err)
		os.Exit(1)
	}
}
