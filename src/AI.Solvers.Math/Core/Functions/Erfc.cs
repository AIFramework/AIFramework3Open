using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Дополнительная функция ошибок
public class Erfc : Expression
{
    public Expression Argument { get; }

    public Erfc(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        var coefficient = new Constant(-2.0 / System.Math.Sqrt(System.Math.PI));
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

        // Округление хвоста здесь недопустимо: erfc(5) = 1.54e-12, erfc(10) = 2.09e-45 —
        // это осмысленные значения, а прежние правила x > 5 -> 0 и x < -5 -> 2
        // обнуляли ровно ту область, ради которой берут erfc вместо 1 - erf.
        if (arg is Constant c && c.Value == 0)
            return new Constant(1);

        return new Erfc(arg);
    }

    public override string ToString() => $"erfc({Argument})";

    public override Expression Clone() => new Erfc(Argument.Clone());
}
