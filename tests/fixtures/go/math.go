package math

// Add returns the sum of a and b.
func Add(a int, b int) int {
	return a + b
}

// Greet returns a greeting for the given name.
func Greet(name string) string {
	return "Hello, " + name + "!"
}

// internal should NOT be extracted (unexported).
func internal() {}
