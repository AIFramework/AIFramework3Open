using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Функция ошибок
public class Erf : Expression
{
    public Expression Argument { get; }

    public Erf(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        var coefficient = new Constant(2.0 / System.Math.Sqrt(System.Math.PI));
        var exponential = new Exp(
            new Multiply(
                new Constant(-1),
                new Power(Argument, new Constant(2))
            )
        );

        return new Multiply(
            new Multiply(coefficient, exponential),
            Argument.Derivative(variable)
        );
    }

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        // erf(6) = 1 - 2.2e-17 — в double уже неотличимо от единицы; на 5 округлять рано:
        // там ещё 1.5e-12, и знание этого хвоста иногда и есть цель вычисления.
        if (arg is Constant c)
        {
            if (c.Value == 0) return new Constant(0);
            if (c.Value >= 6) return new Constant(1);
            if (c.Value <= -6) return new Constant(-1);
        }

        return new Erf(arg);
    }

    public override string ToString() => $"erf({Argument})";

    public override Expression Clone() => new Erf(Argument.Clone());
}
