package fixtures

func Add(a, b int) int {
	return a + b
}

func Greet(name string) string {
	return "Hello, " + name
}

func LogMessage(msg string) {
	println(msg)
}

func Validate(input string) error {
	return nil
}

func Divide(a, b float64) (float64, error) {
	return a / b, nil
}
