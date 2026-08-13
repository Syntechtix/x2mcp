def add(a: int, b: int) -> int:
    return a + b


def greet(first: str, last: str = "") -> str:
    return f"Hello, {first} {last}".strip()


def ping() -> None:
    return None


async def fetch(url: str) -> str:
    return url


def variadic(*items: int) -> list[int]:
    return list(items)


def transform(payload: dict[str, list[int]] = {"a": [1, 2]}) -> dict[str, list[int]]:
    return payload
