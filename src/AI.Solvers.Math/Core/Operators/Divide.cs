using AI.Solvers.Math.CAS;
using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Operators;

// Деление (дробь): a/b
public class Divide : Expression
{
    public Expression Numerator { get; }
    public Expression Denominator { get; }

    public Divide(Expression numerator, Expression denominator)
    {
        Numerator = numerator;
        Denominator = denominator;
    }

    public override Expression Derivative(string variable)
    {
        // (u/v)' = (u'v - uv')/v²
        var uPrime = Numerator.Derivative(variable);
        var vPrime = Denominator.Derivative(variable);

        return new Divide(
            new Add(
                new Multiply(uPrime, Denominator),
                new Multiply(
                    new Constant(-1),
                    new Multiply(Numerator, vPrime)
                )
            ),
            new Power(Denominator, new Constant(2))
        );
    }

    public override Expression Simplify()
    {
        var num = Numerator.Simplify();
        var den = Denominator.Simplify();

        // 0/x = 0
        if (num is Constant c1 && System.Math.Abs(c1.Value) < 1e-10)
            return new Constant(0);

        // x/1 = x
        if (den is Constant c2 && System.Math.Abs(c2.Value - 1) < 1e-10)
            return num;

        // x/x = 1
        if (AlgebraicSimplifier.ExpressionsEqual(num, den))
            return new Constant(1);

        // a/b (константы)
        if (num is Constant cn && den is Constant cd)
            return new Constant(cn.Value / cd.Value);

        // (a*x)/(b*x) = a/b
        if (num is Multiply multNum && den is Multiply multDen)
        {
            if (AlgebraicSimplifier.ExpressionsEqual(multNum.Right, multDen.Right))
            {
                return new Divide(multNum.Left, multDen.Left).Simplify();
            }
        }

        // x/(a*x) = 1/a
        if (den is Multiply multDen2 && AlgebraicSimplifier.ExpressionsEqual(num, multDen2.Right))
        {
            return new Divide(new Constant(1), multDen2.Left).Simplify();
        }

        // (a*x)/x = a
        if (num is Multiply multNum2 && AlgebraicSimplifier.ExpressionsEqual(multNum2.Right, den))
        {
            return multNum2.Left.Simplify();
        }

        return new Divide(num, den);
    }

    public override string ToString()
    {
        string numStr = Numerator is Add || Numerator is Multiply && Numerator.ToString().Contains("+")
            ? $"({Numerator})"
            : Numerator.ToString();

        string denStr = Denominator is Add || Denominator is Multiply || Denominator is Divide
            ? $"({Denominator})"
            : Denominator.ToString();

        return $"{numStr}/{denStr}";
    }

    public override Expression Clone() => new Divide(Numerator.Clone(), Denominator.Clone());
}
