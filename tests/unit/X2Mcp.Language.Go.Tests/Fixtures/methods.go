package fixtures

type Calculator struct{}

type CalculatorGeneric[T any] struct{}

func (c Calculator) Add(a, b int) int {
	return a + b
}

func (c *Calculator) Multiply(a, b int) int {
	return a * b
}

func (c CalculatorGeneric[T]) Generic(a int) int {
	return a
}

func (c Calculator) hidden() int {
	return 0
}

type internalCalculator struct{}

func (c internalCalculator) PublicButInternalType() int {
	return 1
}
