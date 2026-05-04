using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Секанс
public class Sec : Expression
{
    public Expression Argument { get; }

    public Sec(Expression argument) => Argument = argument;

    // d/dx sec(x) = sec(x)tan(x)
    public override Expression Derivative(string variable) =>
        new Multiply(
            new Multiply(this, new Tan(Argument)),
            Argument.Derivative(variable)
        );

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c)
            return new Constant(1.0 / System.Math.Cos(c.Value));

        return new Sec(arg);
    }

    public override string ToString() => $"sec({Argument})";

    public override Expression Clone() => new Sec(Argument.Clone());
}
