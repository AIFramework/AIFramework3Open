using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Интеграл Френеля S
public class FresnelS : Expression
{
    public Expression Argument { get; }

    public FresnelS(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        // d/dx S(f(x)) = sin(π·f(x)²/2) * f'(x)
        return new Multiply(
            new Sin(
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

        // S(0) = 0
        if (arg is Constant c && System.Math.Abs(c.Value) < 1e-10)
            return new Constant(0);

        return new FresnelS(arg);
    }

    public override string ToString() => $"FresnelS({Argument})";

    public override Expression Clone() => new FresnelS(Argument.Clone());
}

