pub fn add(a: i32, b: i32) -> i32 {
    a + b
}

pub fn greet(name: String) -> String {
    format!("Hello, {}!", name)
}

pub fn log_message(msg: String) {
    println!("{}", msg);
}

pub fn validate(input: String) -> Result<(), String> {
    Ok(())
}

pub fn divide(a: f64, b: f64) -> Result<f64, String> {
    if b == 0.0 {
        return Err("division by zero".to_string());
    }
    Ok(a / b)
}

pub fn format_value(value: f64, format: Option<String>) -> String {
    format!("{:?} {:?}", value, format)
}
