using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Десятичный логарифм
public class Log10 : Expression
{
    public Expression Argument { get; }

    public Log10(Expression argument) => Argument = argument;

    // d/dx log10(x) = 1/(x * ln(10))
    public override Expression Derivative(string variable) =>
        new Multiply(
            new Power(
                new Multiply(
                    Argument,
                    new Constant(System.Math.Log(10))
                ),
                new Constant(-1)
            ),
            Argument.Derivative(variable)
        );

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c && c.Value == 1)
            return new Constant(0);

        if (arg is Constant c2 && c2.Value > 0)
            return new Constant(System.Math.Log10(c2.Value));

        return new Log10(arg);
    }

    public override string ToString() => $"log10({Argument})";

    public override Expression Clone() => new Log10(Argument.Clone());
}
