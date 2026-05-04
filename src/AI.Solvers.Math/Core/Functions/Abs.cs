using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;
using System;

namespace AI.Solvers.Math.Core.Functions;

// Абсолютное значение
public class Abs : Expression
{
    public Expression Argument { get; }

    public Abs(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        return new Multiply(
            Argument.Derivative(variable),
            new Multiply(
                Argument,
                new Power(new Abs(Argument), new Constant(-1))
            )
        );
    }

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();

        if (arg is Constant c)
            return new Constant(System.Math.Abs(c.Value));

        if (arg is Power pow && pow.Exponent is Constant exp && exp.Value % 2 == 0)
            return arg;

        return new Abs(arg);
    }

    public override string ToString() => $"abs({Argument})";

    public override Expression Clone() => new Abs(Argument.Clone());
}