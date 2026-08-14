pub struct Calculator;

impl Calculator {
    pub fn add(&self, a: i32, b: i32) -> i32 {
        a + b
    }

    pub fn multiply(&self, a: i32, b: i32) -> i32 {
        a * b
    }

    fn hidden(&self) -> i32 {
        0
    }
}

struct InternalCalculator;

impl InternalCalculator {
    pub fn public_but_internal_type(&self) -> i32 {
        1
    }
}
