using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Гиперболический тангенс
public class Tanh : Expression
{
    public Expression Argument { get; }

    public Tanh(Expression argument) => Argument = argument;


    public override Expression Derivative(string variable) =>
        new Multiply(
            new Power(new Cosh(Argument), new Constant(-2)),
            Argument.Derivative(variable)
        );

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c && c.Value == 0)
            return new Constant(0);

        if (arg is Constant c2)
            return new Constant(System.Math.Tanh(c2.Value));

        return new Tanh(arg);
    }

    public override string ToString() => $"tanh({Argument})";

    public override Expression Clone() => new Tanh(Argument.Clone());
}
