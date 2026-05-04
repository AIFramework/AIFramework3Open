using AI.Solvers.Math.Core.Parsers;
using System.Numerics;

namespace AI.Solvers.Math.Core;

public class ComplexConstant : Expression
{
    public Complex Value { get; }

    public ComplexConstant(double real, double imaginary = 0)
    {
        Value = new Complex(real, imaginary);
    }

    public ComplexConstant(Complex value)
    {
        Value = value;
    }

    public override Expression Derivative(string variable) =>
        new ComplexConstant(0, 0);

    public override Expression Simplify() => this;

    public override string ToString()
    {
        if (System.Math.Abs(Value.Imaginary) < 1e-10)
            return Value.Real.ToString("F6");

        if (System.Math.Abs(Value.Real) < 1e-10)
            return $"{Value.Imaginary:F6}i";

        string sign = Value.Imaginary >= 0 ? "+" : "";
        return $"({Value.Real:F6}{sign}{Value.Imaginary:F6}i)";
    }

    public override Expression Clone() => new ComplexConstant(Value);
}
