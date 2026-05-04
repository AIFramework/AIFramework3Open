using System.Text;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class AdvancedSolver
{
    #region Пределы

    public static string ComputeLimit(string expression, string variable, string point)
    {
        try
        {
            var expr = AdvancedMathExpression.Parse(expression);
            double pointValue;

            if (point == "0")
                pointValue = 0;
            else if (point == "infinity" || point == "inf" || point == "∞")
                return ComputeLimitAtInfinity(expr, variable, true);
            else if (point == "-infinity" || point == "-inf")
                return ComputeLimitAtInfinity(expr, variable, false);
            else if (point == "pi")
                pointValue = System.Math.PI;
            else if (point == "e")
                pointValue = System.Math.E;
            else
                pointValue = double.Parse(point, System.Globalization.CultureInfo.InvariantCulture);

            var vars = new Dictionary<string, double> { { variable, pointValue } };
            try
            {
                double result = EvaluateExpression(expr, vars);
                if (!double.IsNaN(result) && !double.IsInfinity(result))
                    return result.ToString("G6");
            }
            catch
            {
                // Прямая подстановка может бросить исключение для выражений с
                // делением на ноль, log(0), 0^0 и т.п. — это ожидаемая
                // неопределённость, поэтому переходим к правилу Лопиталя.
            }

            return ApplyLHopital(expr, variable, pointValue);
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    private static string ComputeLimitAtInfinity(Expression expr, string variable, bool positive)
    {
        var testValue = positive ? 1e10 : -1e10;
        var vars = new Dictionary<string, double> { { variable, testValue } };
        try
        {
            double result = EvaluateExpression(expr, vars);
            if (System.Math.Abs(result) < 1e-6) return "0";
            if (double.IsInfinity(result)) return result > 0 ? "∞" : "-∞";
            return result.ToString("G6");
        }
        catch
        {
            // Числовой подстановочный метод не справляется (переполнение,
            // неопределённость) — отдаём пользователю осмысленную диагностику
            // вместо невнятной ошибки.
            return "Предел требует более сложного анализа";
        }
    }

    // Правило Лопиталя: для f/g, если f(x0)=g(x0)=0 (или ±∞),
    // то lim f/g = lim f'/g'. Применяем итеративно до нескольких раз.
    private static string ApplyLHopital(Expression expr, string variable, double point)
    {
        const int maxIterations = 4;
        Expression current = expr;
        for (int i = 0; i < maxIterations; i++)
        {
            if (current is not Divide div) break;

            // Применяем правило: дифференцируем числитель и знаменатель.
            var numD = div.Numerator.Derivative(variable).Simplify();
            var denD = div.Denominator.Derivative(variable).Simplify();
            current  = new Divide(numD, denD);

            try
            {
                var vars = new Dictionary<string, double> { { variable, point } };
                double result = EvaluateExpression(current, vars);
                if (!double.IsNaN(result) && !double.IsInfinity(result))
                    return result.ToString("G6");
            }
            catch
            {
                // Снова неопределённость — повторяем правило.
            }
        }

        // Боковой подход — ε-возмущение. Может расходиться для сильных сингулярностей.
        var epsilon = 1e-4;
        try
        {
            double leftVal  = EvaluateExpression(expr,
                new Dictionary<string, double> { { variable, point - epsilon } });
            double rightVal = EvaluateExpression(expr,
                new Dictionary<string, double> { { variable, point + epsilon } });
            if (System.Math.Abs(leftVal - rightVal) < 1e-3)
                return ((leftVal + rightVal) / 2).ToString("G6");
            return $"Левый предел = {leftVal:G6},  правый = {rightVal:G6} (различаются -> возможна разрывность)";
        }
        catch
        {
            return "Предел требует символьного аналитического вычисления.";
        }
    }

    #endregion

    #region Вспомогательные методы (общие для всех частей)

    /// <summary>
    /// Делегирует к <see cref="ExpressionEvaluator.Evaluate"/> — единственная точка вычисления AST.
    /// </summary>
    internal static double EvaluateExpression(Expression expr, Dictionary<string, double> variables)
        => ExpressionEvaluator.Evaluate(expr, variables);

    /// <summary>
    /// Делегирует к <see cref="ExpressionEvaluator.CollectVariables"/>.
    /// </summary>
    internal static void CollectVariables(Expression expr, HashSet<string> variables)
        => ExpressionEvaluator.CollectVariables(expr, variables);

    #endregion
}
