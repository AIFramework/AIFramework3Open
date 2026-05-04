using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

public class Atan : Expression
{
    public Expression Argument { get; }

    public Atan(Expression argument) => Argument = argument;

    // d/dx atan(x) = 1/(1 + x²)
    public override Expression Derivative(string variable) =>
        new Multiply(
            new Power(
                new Add(
                    new Constant(1),
                    new Power(Argument, new Constant(2))
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

        if (arg is Constant c2)
            return new Constant(System.Math.Atan(c2.Value));

        return new Atan(arg);
    }

    public override string ToString() => $"atan({Argument})";

    public override Expression Clone() => new Atan(Argument.Clone());
}
