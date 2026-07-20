# Example: wrapping a Python script (coming soon)

> **Status:** The Python toolchain is registered (`python`, `pyinstaller`), but
> `PythonScanner` doesn't parse source yet — running `x2mcp` against a `.py` file
> today throws `NotImplementedException`. This doc describes the intended flow.

## Prerequisites

- Python 3 and [PyInstaller](https://pyinstaller.org/) — `python` and
  `pyinstaller` must both be on your `PATH` so `x2mcp` can freeze the generated
  wrapper into a single executable. No minimum Python version is pinned yet.

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
x2mcp ./calculator.py --out ./dist/calculator-mcp --name calculator --transport stdio
```

## 3. Planned toolchain

Once the scanner is implemented, `x2mcp` will emit a wrapper `main.py` that exposes
each public method as an MCP tool, then freeze it into a single executable with:

```bash
pyinstaller --onefile --distpath ./dist/calculator-mcp <generated-project>/main.py
```

Required executables: `python`, `pyinstaller`.
Supported transports: `stdio`, `http`.

Follow [csharp.md](csharp.md) for a full working walkthrough in the meantime.
