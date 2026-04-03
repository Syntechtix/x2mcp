using System.Threading.Tasks;

namespace Fixtures;

public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
    public async Task<double> DivideAsync(double numerator, double denominator)
    {
        await Task.Delay(0);
        return numerator / denominator;
    }
    public string Format(double value, string format = "G") => value.ToString(format);
}

public class Greeter
{
    public string Greet(string name) => $"Hello, {name}!";
}
