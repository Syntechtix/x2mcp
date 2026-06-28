/**
 * TypeScript fixture: a small math utility with exported functions.
 * Used by the extractor tests.
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
 * Greets a person by name.
 * @param name - The person's name
 * @param loud - Whether to shout the greeting
 */
export function greet(name: string, loud?: boolean): string {
  const msg = `Hello, ${name}!`;
  return loud ? msg.toUpperCase() : msg;
}

// This should NOT be extracted (not exported)
function _internal(): void {}

/** Converts Fahrenheit to Celsius. */
export const fahrenheitToCelsius = (f: number): number => ((f - 32) * 5) / 9;
