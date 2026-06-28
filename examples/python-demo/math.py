"""
A simple math utility.
This file is used as a demo for mcpify.

Run: npx mcpify generate ./math.py
"""


def add(a: int, b: int) -> int:
    """Add two integers together.

    Args:
        a (int): First operand.
        b (int): Second operand.
    """
    return a + b


def subtract(a: int, b: int) -> int:
    """Subtract b from a.

    Args:
        a (int): Value to subtract from.
        b (int): Value to subtract.
    """
    return a - b


def fahrenheit_to_celsius(fahrenheit: float) -> float:
    """Convert a temperature from Fahrenheit to Celsius.

    Args:
        fahrenheit (float): Temperature in degrees Fahrenheit.
    """
    return (fahrenheit - 32) * 5 / 9


def greet(name: str, loud: bool = False) -> str:
    """Greet a user by name.

    Args:
        name (str): The user's name.
        loud (bool): Whether to shout the greeting.
    """
    msg = f"Hello, {name}!"
    return msg.upper() if loud else msg
