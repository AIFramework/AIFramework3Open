using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

public class Acos : Expression
{
    public Expression Argument { get; }

    public Acos(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable) =>
        new Multiply(
            new Constant(-1),
            new Multiply(
                new Power(
                    new Add(
                        new Constant(1),
                        new Multiply(
                            new Constant(-1),
                            new Power(Argument, new Constant(2))
                        )
                    ),
                    new Constant(-0.5)
                ),
                Argument.Derivative(variable)
            )
        );

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c && c.Value == 1)
            return new Constant(0);

        if (arg is Constant c2 && System.Math.Abs(c2.Value) <= 1)
            return new Constant(System.Math.Acos(c2.Value));

        return new Acos(arg);
    }

    public override string ToString() => $"acos({Argument})";

    public override Expression Clone() => new Acos(Argument.Clone());
}
