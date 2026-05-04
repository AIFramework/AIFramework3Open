using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Тангенс
public class Tan : Expression
{
    public Expression Argument { get; }

    public Tan(Expression argument) => Argument = argument;

    
    public override Expression Derivative(string variable) =>
        new Multiply(
            new Power(new Cos(Argument), new Constant(-2)),
            Argument.Derivative(variable)
        );

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c && c.Value == 0)
            return new Constant(0);

        if (arg is Constant c2)
            return new Constant(System.Math.Tan(c2.Value));

        return new Tan(arg);
    }

    public override string ToString() => $"tan({Argument})";

    public override Expression Clone() => new Tan(Argument.Clone());
}
