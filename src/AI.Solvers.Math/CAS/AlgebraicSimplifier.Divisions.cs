using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.CAS;

public static partial class AlgebraicSimplifier
{
    #region Упрощение дробей

    private static Expression SimplifyDivisions(Expression expr)
    {
        switch (expr)
        {
            case Multiply mult:
                return new Multiply(SimplifyDivisions(mult.Left), SimplifyDivisions(mult.Right));
            case Power pow:
                return new Power(SimplifyDivisions(pow.Base), SimplifyDivisions(pow.Exponent));
            case Add add:
                return TryCombineFractions(add) ?? new Add(SimplifyDivisions(add.Left), SimplifyDivisions(add.Right));
            case Divide div:
                return SimplifyFraction(div);
            default:
                return expr;
        }
    }

    private static Expression? TryCombineFractions(Add add)
    {
        var terms     = new List<Expression>();
        CollectAddTerms(add, terms);
        var divisions = terms.OfType<Divide>().ToList();
        if (divisions.Count < 2) return null;

        Expression commonDenom = divisions[0].Denominator;
        foreach (var d in divisions.Skip(1))
            commonDenom = new Multiply(commonDenom, d.Denominator);

        Expression? numeratorSum = null;
        foreach (var term in terms)
        {
            Expression newNum;
            if (term is Divide div)
            {
                // Для слагаемого N_i / D_i множитель к числителю должен быть
                // произведением всех ОСТАЛЬНЫХ знаменателей: C / D_i = ∏_{j≠i} D_j.
                Expression? mult = null;
                foreach (var od in divisions)
                {
                    if (ReferenceEquals(od, div)) continue;
                    mult = mult is null ? od.Denominator : new Multiply(mult, od.Denominator);
                }
                newNum = mult is null
                    ? div.Numerator.Simplify()
                    : new Multiply(div.Numerator, mult).Simplify();
            }
            else
            {
                newNum = new Multiply(term, commonDenom).Simplify();
            }
            numeratorSum = numeratorSum is null ? newNum : new Add(numeratorSum, newNum);
        }

        return numeratorSum is null ? null : new Divide(numeratorSum.Simplify(), commonDenom.Simplify());
    }

    private static Expression SimplifyFraction(Divide div)
    {
        var num = SimplifyDivisions(div.Numerator);
        var den = SimplifyDivisions(div.Denominator);

        if (num is not Multiply || den is not Multiply) return new Divide(num, den);

        var numFactors = new List<Expression>();
        var denFactors = new List<Expression>();
        CollectMultiplyFactors(num, numFactors);
        CollectMultiplyFactors(den, denFactors);

        for (int i = numFactors.Count - 1; i >= 0; i--)
            for (int j = denFactors.Count - 1; j >= 0; j--)
                if (ExpressionsEqual(numFactors[i], denFactors[j]))
                {
                    numFactors.RemoveAt(i);
                    denFactors.RemoveAt(j);
                    break;
                }

        Expression? newNum = numFactors.Count > 0
            ? numFactors.Aggregate((a, b) => (Expression)new Multiply(a, b)) : null;
        Expression? newDen = denFactors.Count > 0
            ? denFactors.Aggregate((a, b) => (Expression)new Multiply(a, b)) : null;

        if (newNum is not null && newDen is not null) return new Divide(newNum, newDen);
        if (newNum is not null) return newNum;
        if (newDen is not null) return new Divide(new Constant(1), newDen);
        return new Constant(1);
    }

    #endregion
}
