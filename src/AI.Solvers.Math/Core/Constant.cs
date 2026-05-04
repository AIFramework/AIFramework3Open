using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core;

public class Constant : Expression
{
    public double Value { get; }

    public Constant(double value)
    {
        Value = value;
    }

    public override Expression Derivative(string variable) => new Constant(0);

    public override Expression Simplify() => this;

    public override string ToString()
    {
        // Целые числа без дробной части
        if (System.Math.Abs(Value - System.Math.Round(Value)) < 1e-10)
        {
            return ((int)System.Math.Round(Value)).ToString();
        }

        // Дробные числа с точкой
        return Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }

    public override Expression Clone() => new Constant(Value);
}
