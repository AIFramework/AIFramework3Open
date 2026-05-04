using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Гиперболический синус: sinh(x) = (e^x - e^(-x))/2
public class Sinh : Expression
{
    public Expression Argument { get; }

    public Sinh(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable) =>
        new Multiply(new Cosh(Argument), Argument.Derivative(variable));

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c && c.Value == 0)
            return new Constant(0);

        if (arg is Constant c2)
            return new Constant(System.Math.Sinh(c2.Value));

        return new Sinh(arg);
    }

    public override string ToString() => $"sinh({Argument})";

    public override Expression Clone() => new Sinh(Argument.Clone());
}
