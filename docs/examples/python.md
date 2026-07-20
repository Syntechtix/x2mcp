# Example: wrapping a Python script (coming soon)

> **Status:** The Python toolchain is registered (`python`, `pyinstaller`), but
> `PythonScanner` doesn't parse source yet — running `mcpify` against a `.py` file
> today throws `NotImplementedException`. This doc describes the intended flow.

## 1. The source file

```python
# calculator.py
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

    def subtract(self, a: int, b: int) -> int:
        return a - b
```

## 2. Planned invocation

```bash
mcpify ./calculator.py --out ./dist/calculator-mcp --name calculator --transport stdio
```

## 3. Planned toolchain

Once the scanner is implemented, `mcpify` will emit a wrapper `main.py` that exposes
each public method as an MCP tool, then freeze it into a single executable with:

```bash
pyinstaller --onefile --distpath ./dist/calculator-mcp <generated-project>/main.py
```

Required executables: `python`, `pyinstaller`.
Supported transports: `stdio`, `http`.

Follow [csharp.md](csharp.md) for a full working walkthrough in the meantime.
