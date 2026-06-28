def add(a: int, b: int) -> int:
    """Add two integers together.

    Args:
        a (int): First operand.
        b (int): Second operand.

    Returns:
        int: The sum.
    """
    return a + b


def greet(name: str, loud: bool = False) -> str:
    """Greet a person by name.

    Args:
        name (str): The person's name.
        loud (bool): Whether to shout.
    """
    msg = f"Hello, {name}!"
    return msg.upper() if loud else msg


def _private_helper():
    """This should NOT be extracted."""
    pass
