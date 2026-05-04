using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Знаковая функция: sgn(x) = -1 (x<0), 0 (x=0), +1 (x>0)
public class Sgn : Expression
{
    public Expression Argument { get; }

    public Sgn(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        // d/dx sgn(x) = 2*δ(x) - технически производная не существует, но формально это 2*delta(x)
        return new Constant(0);
    }

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();
        if (arg is Constant c)
        {
            if (c.Value > 0) return new Constant(1);
            if (c.Value < 0) return new Constant(-1);
            return new Constant(0);
        }
        return new Sgn(arg);
    }

    public override string ToString() => $"sgn({Argument})";

    public override Expression Clone() => new Sgn(Argument.Clone());
}

