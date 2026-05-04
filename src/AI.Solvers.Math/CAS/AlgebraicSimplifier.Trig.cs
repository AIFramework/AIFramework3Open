using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.CAS;

public static partial class AlgebraicSimplifier
{
    #region Тригонометрические тождества

    private static Expression ApplyTrigonometricIdentities(Expression expr)
    {
        switch (expr)
        {
            case Add add:
                // sin²(x) + cos²(x) = 1
                if (IsSinSquared(add.Left,  out var sinArg) && IsCosSquared(add.Right, out var cosArg) && ExpressionsEqual(sinArg, cosArg)) return new Constant(1);
                if (IsCosSquared(add.Left,  out cosArg)     && IsSinSquared(add.Right, out sinArg)     && ExpressionsEqual(sinArg, cosArg)) return new Constant(1);
                return new Add(ApplyTrigonometricIdentities(add.Left), ApplyTrigonometricIdentities(add.Right));
            case Multiply mult:
                return new Multiply(ApplyTrigonometricIdentities(mult.Left), ApplyTrigonometricIdentities(mult.Right));
            case Divide div:
                return new Divide(ApplyTrigonometricIdentities(div.Numerator), ApplyTrigonometricIdentities(div.Denominator));
            case Power pow:
                return new Power(ApplyTrigonometricIdentities(pow.Base), ApplyTrigonometricIdentities(pow.Exponent));
            default:
                return expr;
        }
    }

    private static bool IsSinSquared(Expression expr, out Expression? argument)
    {
        argument = null;
        if (expr is Power pow && pow.Exponent is Constant c && System.Math.Abs(c.Value - 2) < 1e-10 && pow.Base is Sin sin)
        { argument = sin.Argument; return true; }
        return false;
    }

    private static bool IsCosSquared(Expression expr, out Expression? argument)
    {
        argument = null;
        if (expr is Power pow && pow.Exponent is Constant c && System.Math.Abs(c.Value - 2) < 1e-10 && pow.Base is Cos cos)
        { argument = cos.Argument; return true; }
        return false;
    }

    #endregion
}
