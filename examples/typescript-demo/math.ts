/**
 * A simple math utility.
 * This file is used as a demo for `mcpify`.
 *
 * Run: npx mcpify generate ./math.ts
 */

/**
 * Adds two numbers together.
 * @param a - First operand
 * @param b - Second operand
 */
export function add(a: number, b: number): number {
  return a + b;
}

/**
 * Subtracts b from a.
 * @param a - Value to subtract from
 * @param b - Value to subtract
 */
export function subtract(a: number, b: number): number {
  return a - b;
}

/**
 * Converts a temperature from Fahrenheit to Celsius.
 * @param fahrenheit - Temperature in degrees Fahrenheit
 */
export function fahrenheitToCelsius(fahrenheit: number): number {
  return ((fahrenheit - 32) * 5) / 9;
}

/**
 * Greets a user by name.
 * @param name - The user's name
 * @param loud - Whether to shout the greeting
 */
export function greet(name: string, loud?: boolean): string {
  const msg = `Hello, ${name}!`;
  return loud ? msg.toUpperCase() : msg;
}
