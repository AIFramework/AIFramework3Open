using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Функция Хевисайда (единичная ступенька)
public class Heaviside : Expression
{
    public Expression Argument { get; }

    public Heaviside(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        // d/dx H(x) = delta(x) - дельта-функция Дирака
        return new Constant(0);
    }

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();
        if (arg is Constant c)
        {
            return new Constant(c.Value >= 0 ? 1 : 0);
        }
        return new Heaviside(arg);
    }

    public override string ToString() => $"H({Argument})";

    public override Expression Clone() => new Heaviside(Argument.Clone());
}

