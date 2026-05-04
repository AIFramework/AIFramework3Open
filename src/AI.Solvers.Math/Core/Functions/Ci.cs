using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Интегральный косинус
public class Ci : Expression
{
    public Expression Argument { get; }

    public Ci(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        // d/dx Ci(f(x)) = cos(f(x))/f(x) * f'(x)
        return new Multiply(
            new Multiply(
                new Cos(Argument),
                new Power(Argument, new Constant(-1))
            ),
            Argument.Derivative(variable)
        );
    }

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();
        return new Ci(arg);
    }

    public override string ToString() => $"Ci({Argument})";

    public override Expression Clone() => new Ci(Argument.Clone());
}

