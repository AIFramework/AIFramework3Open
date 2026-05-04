using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.CAS;

public static partial class AlgebraicSimplifier
{
    #region Объединение степеней одного основания

    private static Expression CombinePowersInMultiply(Expression expr)
    {
        switch (expr)
        {
            case Add add:
                return new Add(CombinePowersInMultiply(add.Left), CombinePowersInMultiply(add.Right));
            case Divide div:
                return new Divide(CombinePowersInMultiply(div.Numerator), CombinePowersInMultiply(div.Denominator));
            case Power pow:
                return new Power(CombinePowersInMultiply(pow.Base), CombinePowersInMultiply(pow.Exponent));
            case Multiply:
                return MergePowers(expr);
            default:
                return expr;
        }
    }

    private static Expression MergePowers(Expression expr)
    {
        var factors  = new List<Expression>();
        CollectMultiplyFactors(expr, factors);

        var powerGroups = new Dictionary<string, (Expression baseExpr, double totalPower)>();
        var constants   = new List<double>();

        foreach (var factor in factors)
        {
            if (factor is Constant c)
            {
                constants.Add(c.Value);
            }
            else if (factor is Power pow && pow.Exponent is Constant ce)
            {
                string key = pow.Base.ToString();
                if (powerGroups.TryGetValue(key, out var existing))
                    powerGroups[key] = (existing.baseExpr, existing.totalPower + ce.Value);
                else
                    powerGroups[key] = (pow.Base, ce.Value);
            }
            else
            {
                string key = factor.ToString();
                if (powerGroups.TryGetValue(key, out var existing))
                    powerGroups[key] = (existing.baseExpr, existing.totalPower + 1);
                else
                    powerGroups[key] = (factor, 1);
            }
        }

        double constProduct = constants.Count > 0 ? constants.Aggregate(1.0, (a, b) => a * b) : 1.0;
        Expression? result  = System.Math.Abs(constProduct - 1) > 1e-10 ? new Constant(constProduct) : null;

        foreach (var (baseExpr, totalPower) in powerGroups.Values)
        {
            if (System.Math.Abs(totalPower) < 1e-10) continue;
            Expression powerExpr = System.Math.Abs(totalPower - 1) < 1e-10
                ? baseExpr : new Power(baseExpr, new Constant(totalPower));
            result = result is null ? powerExpr : new Multiply(result, powerExpr);
        }

        return result ?? new Constant(constProduct);
    }

    #endregion
}
