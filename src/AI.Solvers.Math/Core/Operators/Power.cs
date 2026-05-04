using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Operators;

public class Power : Expression
{
    public Expression Base { get; }
    public Expression Exponent { get; }

    public Power(Expression baseExpr, Expression exponent)
    {
        Base = baseExpr;
        Exponent = exponent;
    }

    public override Expression Derivative(string variable)
    {
        // Если показатель - константа: (f^n)' = n * f^(n-1) * f'
        if (Exponent is Constant)
        {
            return new Multiply(
                new Multiply(
                    Exponent,
                    new Power(Base, new Add(Exponent, new Constant(-1)))
                ),
                Base.Derivative(variable)
            );
        }

        // Общий случай: (f^g)' = f^g * [g' * ln(f) + g * f'/f]
        // Это эквивалентно производной e^(g*ln(f))
        var gPrime = Exponent.Derivative(variable);
        var fPrime = Base.Derivative(variable);

        // f^g * [g' * ln(f) + g * f'/f]
        var lnF = new Ln(Base);
        var term1 = new Multiply(gPrime, lnF);                    // g' * ln(f)
        var term2 = new Multiply(
            Exponent,
            new Multiply(fPrime, new Power(Base, new Constant(-1)))  // g * f' / f
        );

        return new Multiply(
            this,  // f^g
            new Add(term1, term2)  // g'*ln(f) + g*f'/f
        );
    }

    public override Expression Simplify()
    {
        var baseExpr = Base.Simplify();
        var exponent = Exponent.Simplify();

        // СПЕЦИАЛЬНОЕ ПРЕОБРАЗОВАНИЕ: e^x -> exp(x)
        // Если основание - это число Эйлера (константа ≈ 2.71828)
        if (baseExpr is Constant cb && System.Math.Abs(cb.Value - System.Math.E) < 1e-10)
        {
            return new Exp(exponent).Simplify();
        }

        if (exponent is Constant c1 && System.Math.Abs(c1.Value) < 1e-10)
            return new Constant(1);

        if (exponent is Constant c2 && System.Math.Abs(c2.Value - 1) < 1e-10)
            return baseExpr;

        if (baseExpr is Constant cb2 && exponent is Constant ce)
            return new Constant(System.Math.Pow(cb2.Value, ce.Value));

        if (baseExpr is Constant c3 && System.Math.Abs(c3.Value) < 1e-10 &&
            exponent is Constant c4 && c4.Value > 0)
            return new Constant(0);

        if (baseExpr is Constant c5 && System.Math.Abs(c5.Value - 1) < 1e-10)
            return new Constant(1);

        if (baseExpr is Constant c6 && System.Math.Abs(c6.Value + 1) < 1e-10 &&
            exponent is Constant c7 && System.Math.Abs(c7.Value % 2) < 1e-10)
            return new Constant(1);

        if (baseExpr is Constant c8 && System.Math.Abs(c8.Value + 1) < 1e-10 &&
            exponent is Constant c9 && System.Math.Abs((c9.Value - 1) % 2) < 1e-10)
            return new Constant(-1);

        return new Power(baseExpr, exponent);
    }

    public override string ToString()
    {
        string baseStr = Base.ToString();
        string expStr = Exponent.ToString();

        if (Base is Add || Base is Multiply)
            baseStr = $"({baseStr})";

        if (Exponent is Multiply mult && mult.Left is Constant c && c.Value < 0)
            expStr = $"({expStr})";

        if (Exponent is Constant ce && ce.Value < 0 && System.Math.Abs(ce.Value + 1) < 1e-10)
        {
            return $"1/{baseStr}";
        }

        if (Exponent is Constant ce2 && ce2.Value < 0 && System.Math.Abs(ce2.Value + 2) < 1e-10)
        {
            return $"1/{baseStr}^2";
        }

        return $"{baseStr}^{expStr}";
    }

    public override Expression Clone() => new Power(Base.Clone(), Exponent.Clone());
}
