class Calculator:
    def __init__(self) -> None:
        self.factor = 1

    def add(self, a: int, b: int) -> int:
        return a + b

    async def fetch(self, value: str) -> str:
        return value

    def _private(self) -> int:
        return 0
