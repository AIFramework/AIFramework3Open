using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Косеканс
public class Csc : Expression
{
    public Expression Argument { get; }

    public Csc(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable) =>
        new Multiply(
            new Multiply(
                new Constant(-1),
                new Multiply(this, new Cot(Argument))
            ),
            Argument.Derivative(variable)
        );

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c)
            return new Constant(1.0 / System.Math.Sin(c.Value));

        return new Csc(arg);
    }

    public override string ToString() => $"csc({Argument})";

    public override Expression Clone() => new Csc(Argument.Clone());
}
