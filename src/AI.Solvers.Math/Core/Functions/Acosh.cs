using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

public class Acosh : Expression
{
    public Expression Argument { get; }

    public Acosh(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable) =>
        new Multiply(
            new Power(
                new Add(
                    new Power(Argument, new Constant(2)),
                    new Constant(-1)
                ),
                new Constant(-0.5)
            ),
            Argument.Derivative(variable)
        );

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c && c.Value == 1)
            return new Constant(0);

        if (arg is Constant c2 && c2.Value >= 1)
            return new Constant(System.Math.Log(c2.Value + System.Math.Sqrt(c2.Value * c2.Value - 1)));

        return new Acosh(arg);
    }

    public override string ToString() => $"acosh({Argument})";

    public override Expression Clone() => new Acosh(Argument.Clone());
}
