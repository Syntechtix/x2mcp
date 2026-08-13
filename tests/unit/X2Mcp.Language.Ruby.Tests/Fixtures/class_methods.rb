class Calculator
  def add(a, b)
    a + b
  end

  private

  def hidden(value)
    value
  end

  public

  def multiply(a, b)
    a * b
  end

  protected

  def limited(value)
    value
  end
end
