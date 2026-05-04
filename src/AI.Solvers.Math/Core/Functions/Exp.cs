using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

public class Exp : Expression
{
    public Expression Argument { get; }

    public Exp(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable) =>
        new Multiply(this, Argument.Derivative(variable));

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        // e^0 = 1
        if (arg is Constant c && c.Value == 0)
            return new Constant(1);

        if (arg is Constant c2)
            return new Constant(System.Math.Exp(c2.Value));

        return new Exp(arg);
    }

    public override string ToString() => $"exp({Argument})";

    public override Expression Clone() => new Exp(Argument.Clone());
}