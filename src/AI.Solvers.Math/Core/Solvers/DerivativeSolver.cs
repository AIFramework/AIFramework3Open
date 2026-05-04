using AI.Solvers.Math.CAS;

namespace AI.Solvers.Math.Core.Solvers;

public static class DerivativeSolver
{
    public static string FirstDerivative(string expression, string variable = "x")
    {
        try
        {
            var expr = AdvancedMathExpression.Parse(expression);
            var derivative = expr.Derivative(variable).Simplify();
            derivative = AlgebraicSimplifier.Simplify(derivative); // Упрощение через CAS
            return derivative.ToString();
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    public static string NthDerivative(string expression, string variable, int order)
    {
        try
        {
            var expr = AdvancedMathExpression.Parse(expression);

            for (int i = 0; i < order; i++)
            {
                expr = expr.Derivative(variable);
                expr = expr.Simplify();
            }

            expr = AlgebraicSimplifier.Simplify(expr);

            return expr.ToString();
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    public static string PartialDerivative(string expression, string variable)
    {
        return FirstDerivative(expression, variable);
    }
}

