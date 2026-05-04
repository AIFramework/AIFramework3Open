using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.CAS;

/// <summary>Система компьютерной алгебры (Computer Algebra System).</summary>
public static partial class AlgebraicSimplifier
{
    #region Главный метод

    public static Expression Simplify(Expression expr, int maxIterations = 10)
    {
        Expression previous;
        int iteration = 0;
        do
        {
            previous = expr;
            expr = ExpandConstantProducts(expr);
            expr = ConvertNegativePowersToDivisions(expr);
            expr = CombinePowersInMultiply(expr);
            expr = SimplifyDivisions(expr);
            expr = ApplyTrigonometricIdentities(expr);
            expr = expr.Simplify();
            expr = CollectLikeTerms(expr);
            expr = FactorCommonTerms(expr);
            iteration++;
        } while (!ExpressionsEqual(expr, previous) && iteration < maxIterations);
        return expr;
    }

    #endregion

    #region Раскрытие произведений констант

    private static Expression ExpandConstantProducts(Expression expr)
    {
        return expr switch
        {
            Add add      => new Add(ExpandConstantProducts(add.Left), ExpandConstantProducts(add.Right)),
            Divide div   => new Divide(ExpandConstantProducts(div.Numerator), ExpandConstantProducts(div.Denominator)),
            Power pow    => new Power(ExpandConstantProducts(pow.Base), ExpandConstantProducts(pow.Exponent)),
            Sin sin      => new Sin(ExpandConstantProducts(sin.Argument)),
            Cos cos      => new Cos(ExpandConstantProducts(cos.Argument)),
            Multiply     => MergeConstants(expr),
            _            => expr
        };
    }

    private static Expression MergeConstants(Expression expr)
    {
        var left  = ExpandConstantProducts(((Multiply)expr).Left);
        var right = ExpandConstantProducts(((Multiply)expr).Right);
        var (coeff, parts) = ExtractAllConstants(new Multiply(left, right));

        if (parts.Count == 0) return new Constant(coeff);

        Expression result = parts[0];
        for (int i = 1; i < parts.Count; i++) result = new Multiply(result, parts[i]);

        return System.Math.Abs(coeff - 1) > 1e-10 ? new Multiply(new Constant(coeff), result) : result;
    }

    private static (double coeff, List<Expression> nonConstParts) ExtractAllConstants(Expression expr)
    {
        double totalCoeff = 1.0;
        var parts = new List<Expression>();

        void Collect(Expression e)
        {
            if (e is Constant c)       totalCoeff *= c.Value;
            else if (e is Multiply m) { Collect(m.Left); Collect(m.Right); }
            else                       parts.Add(e);
        }

        Collect(expr);
        return (totalCoeff, parts);
    }

    #endregion

    #region Конвертация отрицательных степеней в дроби

    private static Expression ConvertNegativePowersToDivisions(Expression expr)
    {
        switch (expr)
        {
            case Add add:
                return new Add(ConvertNegativePowersToDivisions(add.Left),
                               ConvertNegativePowersToDivisions(add.Right));
            case Multiply mult:
            {
                var l = ConvertNegativePowersToDivisions(mult.Left);
                var r = ConvertNegativePowersToDivisions(mult.Right);
                // Симметрично проверяем оба сомножителя — иначе вид x^(-1)*y
                // c левым отрицательным Power не сворачивается в y/x.
                if (r is Power powR && powR.Exponent is Constant cR && cR.Value < 0)
                    return new Divide(l, new Power(powR.Base, new Constant(-cR.Value)));
                if (l is Power powL && powL.Exponent is Constant cL && cL.Value < 0)
                    return new Divide(r, new Power(powL.Base, new Constant(-cL.Value)));
                return new Multiply(l, r);
            }
            case Divide div:
                return new Divide(ConvertNegativePowersToDivisions(div.Numerator),
                                  ConvertNegativePowersToDivisions(div.Denominator));
            case Power pow:
                if (pow.Exponent is Constant ce && ce.Value < 0)
                    return new Divide(new Constant(1),
                        new Power(ConvertNegativePowersToDivisions(pow.Base), new Constant(-ce.Value)));
                return new Power(ConvertNegativePowersToDivisions(pow.Base),
                                 ConvertNegativePowersToDivisions(pow.Exponent));
            case Sin sin: return new Sin(ConvertNegativePowersToDivisions(sin.Argument));
            case Cos cos: return new Cos(ConvertNegativePowersToDivisions(cos.Argument));
            default:      return expr;
        }
    }

    #endregion

    #region Вспомогательные утилиты

    internal static void CollectMultiplyFactors(Expression expr, List<Expression> factors)
    {
        if (expr is Multiply mult) { CollectMultiplyFactors(mult.Left, factors); CollectMultiplyFactors(mult.Right, factors); }
        else factors.Add(expr);
    }

    internal static void CollectAddTerms(Expression expr, List<Expression> terms)
    {
        if (expr is Add add) { CollectAddTerms(add.Left, terms); CollectAddTerms(add.Right, terms); }
        else terms.Add(expr);
    }

    public static bool ExpressionsEqual(Expression? a, Expression? b)
    {
        if (a is null || b is null) return false;
        if (a is Constant ca && b is Constant cb) return System.Math.Abs(ca.Value - cb.Value) < 1e-10;
        if (a is Variable va && b is Variable vb) return va.Name == vb.Name;
        if (a.GetType() != b.GetType()) return false;
        if (a is Add aa && b is Add ab)
            return (ExpressionsEqual(aa.Left, ab.Left) && ExpressionsEqual(aa.Right, ab.Right)) ||
                   (ExpressionsEqual(aa.Left, ab.Right) && ExpressionsEqual(aa.Right, ab.Left));
        if (a is Multiply ma && b is Multiply mb)
            return (ExpressionsEqual(ma.Left, mb.Left) && ExpressionsEqual(ma.Right, mb.Right)) ||
                   (ExpressionsEqual(ma.Left, mb.Right) && ExpressionsEqual(ma.Right, mb.Left));
        if (a is Power pa && b is Power pb)
            return ExpressionsEqual(pa.Base, pb.Base) && ExpressionsEqual(pa.Exponent, pb.Exponent);
        if (a is Sin sa && b is Sin sb) return ExpressionsEqual(sa.Argument, sb.Argument);
        if (a is Cos ca2 && b is Cos cb2) return ExpressionsEqual(ca2.Argument, cb2.Argument);
        if (a is Divide da && b is Divide db)
            return ExpressionsEqual(da.Numerator, db.Numerator) && ExpressionsEqual(da.Denominator, db.Denominator);
        return a.ToString() == b.ToString();
    }

    #endregion
}
