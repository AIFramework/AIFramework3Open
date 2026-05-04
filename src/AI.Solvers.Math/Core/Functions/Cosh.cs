using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Гиперболический косинус: cosh(x)
public class Cosh : Expression
{
    public Expression Argument { get; }

    public Cosh(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable) =>
        new Multiply(new Sinh(Argument), Argument.Derivative(variable));

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c && c.Value == 0)
            return new Constant(1);

        if (arg is Constant c2)
            return new Constant(System.Math.Cosh(c2.Value));

        return new Cosh(arg);
    }

    public override string ToString() => $"cosh({Argument})";

    public override Expression Clone() => new Cosh(Argument.Clone());
}
