using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

public class Atanh : Expression
{
    public Expression Argument { get; }

    public Atanh(Expression argument) => Argument = argument;

    // d/dx atanh(x) = 1/(1 - x²)
    public override Expression Derivative(string variable) =>
        new Multiply(
            new Power(
                new Add(
                    new Constant(1),
                    new Multiply(
                        new Constant(-1),
                        new Power(Argument, new Constant(2))
                    )
                ),
                new Constant(-1)
            ),
            Argument.Derivative(variable)
        );

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c && c.Value == 0)
            return new Constant(0);

        if (arg is Constant c2 && System.Math.Abs(c2.Value) < 1)
            return new Constant(0.5 * System.Math.Log((1 + c2.Value) / (1 - c2.Value)));

        return new Atanh(arg);
    }

    public override string ToString() => $"atanh({Argument})";

    public override Expression Clone() => new Atanh(Argument.Clone());
}
