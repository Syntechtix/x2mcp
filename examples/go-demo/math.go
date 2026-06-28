// Package math provides simple arithmetic utilities.
// This file is used as a demo for mcpify.
//
// Run: npx mcpify generate ./math.go
package math

// Add returns the sum of a and b.
func Add(a int, b int) int {
	return a + b
}

// Subtract returns a minus b.
func Subtract(a int, b int) int {
	return a - b
}

// FahrenheitToCelsius converts a temperature from Fahrenheit to Celsius.
func FahrenheitToCelsius(fahrenheit float64) float64 {
	return (fahrenheit - 32) * 5 / 9
}

// Greet returns a greeting for the given name.
func Greet(name string) string {
	return "Hello, " + name + "!"
}
