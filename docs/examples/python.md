# Example: wrapping a Python module

## Prerequisites

- Python 3 and [PyInstaller](https://pyinstaller.org/) on your `PATH`.
- The Python MCP SDK:

```bash
python -m pip install mcp
```

## 1. The source module

```python
# calculator.py
def add(a: int, b: int) -> int:
  return a + b


def greet(name: str) -> str:
  return f"Hello, {name}"


class Calculator:
  def multiply(self, a: int, b: int) -> int:
    return a * b

  async def echo(self, value: str) -> str:
    return value
```

## 2. Run x2mcp

```bash
x2mcp ./calculator.py --out ./dist/calculator-mcp --name calculator --transport stdio
```

## 3. What gets generated

`x2mcp` emits a `main.py` wrapper plus the source `.py` files the wrapper imports.
The wrapper uses FastMCP and registers top-level functions and class methods as tools.

```python
from mcp.server.fastmcp import FastMCP
import calculator

mcp = FastMCP("calculator")

mcp.tool(name="add")(calculator.add)
mcp.tool(name="greet")(calculator.greet)
_calculator_Calculator = calculator.Calculator()
mcp.tool(name="multiply")(_calculator_Calculator.multiply)
mcp.tool(name="echo")(_calculator_Calculator.echo)

if __name__ == "__main__":
  mcp.run(transport="stdio")
```

## 4. Build

`x2mcp` resolves the toolchain executable from `requiredExecutables`
(`pyinstaller`) and then runs the publish args:

```bash
pyinstaller --onefile --name calculator --distpath ./dist/calculator-mcp <generated-project>/main.py
```

## 5. `--transport http`

Passing `--transport http` switches the entrypoint to streamable HTTP:

```python
if __name__ == "__main__":
  mcp.run(transport="streamable-http")
```
