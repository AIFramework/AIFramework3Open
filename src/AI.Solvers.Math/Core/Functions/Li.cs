using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Интегральный логарифм
public class Li : Expression
{
    public Expression Argument { get; }

    public Li(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        // d/dx li(f(x)) = 1/ln(f(x)) * f'(x)
        return new Multiply(
            new Power(
                new Ln(Argument),
                new Constant(-1)
            ),
            Argument.Derivative(variable)
        );
    }

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();
        return new Li(arg);
    }

    public override string ToString() => $"li({Argument})";

    public override Expression Clone() => new Li(Argument.Clone());
}

