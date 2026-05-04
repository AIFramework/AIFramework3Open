using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;
namespace AI.Solvers.Math.Core.Functions;

// Интегральный синус
public class Si : Expression
{
    public Expression Argument { get; }

    public Si(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        // d/dx Si(f(x)) = sin(f(x))/f(x) * f'(x)
        return new Multiply(
            new Multiply(
                new Sin(Argument),
                new Power(Argument, new Constant(-1))
            ),
            Argument.Derivative(variable)
        );
    }

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        // Si(0) = 0
        if (arg is Constant c && System.Math.Abs(c.Value) < 1e-10)
            return new Constant(0);

        return new Si(arg);
    }

    public override string ToString() => $"Si({Argument})";

    public override Expression Clone() => new Si(Argument.Clone());
}

