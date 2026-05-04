using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Интеграл Френеля
public class FresnelC : Expression
{
    public Expression Argument { get; }

    public FresnelC(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        // d/dx C(f(x)) = cos(π·f(x)²/2) * f'(x)
        return new Multiply(
            new Cos(
                new Multiply(
                    new Constant(System.Math.PI / 2),
                    new Power(Argument, new Constant(2))
                )
            ),
            Argument.Derivative(variable)
        );
    }

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        // C(0) = 0
        if (arg is Constant c && System.Math.Abs(c.Value) < 1e-10)
            return new Constant(0);

        return new FresnelC(arg);
    }

    public override string ToString() => $"FresnelC({Argument})";

    public override Expression Clone() => new FresnelC(Argument.Clone());
}

