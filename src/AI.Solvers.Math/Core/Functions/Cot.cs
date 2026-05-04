using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Котангенс
public class Cot : Expression
{
    public Expression Argument { get; }

    public Cot(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable) =>
        new Multiply(
            new Multiply(
                new Constant(-1),
                new Power(new Sin(Argument), new Constant(-2))
            ),
            Argument.Derivative(variable)
        );

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c)
            return new Constant(1.0 / System.Math.Tan(c.Value));

        return new Cot(arg);
    }

    public override string ToString() => $"cot({Argument})";

    public override Expression Clone() => new Cot(Argument.Clone());
}
