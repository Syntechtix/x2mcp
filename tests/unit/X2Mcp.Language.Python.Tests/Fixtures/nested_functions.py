def outer(value: int) -> int:
    def inner(hidden: int) -> int:
        return hidden

    return value
