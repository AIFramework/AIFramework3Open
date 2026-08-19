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
            if (!TryAsQuotient(current, out var numerator, out var denominator)) break;
            if (!IsIndeterminateQuotient(numerator, denominator, variable, point)) break;

            // Применяем правило: дифференцируем числитель и знаменатель.
            current = new Divide(numerator.Derivative(variable).Simplify(),
                                 denominator.Derivative(variable).Simplify());

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

        return ProbeAroundPoint(expr, variable, point);
    }

    /// <summary>
    /// Видит частное и в узле Divide, и в форме f·g^(-1): парсер кодирует деление
    /// именно так (см. ParseMultiplyDivide), поэтому проверка «is Divide» никогда
    /// не срабатывала для разобранного выражения, и правило Лопиталя не применялось.
    /// </summary>
    private static bool TryAsQuotient(Expression expr, out Expression numerator, out Expression denominator)
    {
        numerator = expr;
        denominator = new Constant(1);

        static bool IsInverse(Expression e, out Expression baseExpr)
        {
            baseExpr = e;
            if (e is Power p && p.Exponent is Constant c && System.Math.Abs(c.Value + 1) < 1e-10)
            {
                baseExpr = p.Base;
                return true;
            }
            return false;
        }

        switch (expr)
        {
            case Divide div:
                numerator = div.Numerator;
                denominator = div.Denominator;
                return true;

            case Multiply mult when IsInverse(mult.Right, out var denRight):
                numerator = mult.Left;
                denominator = denRight;
                return true;

            case Multiply mult when IsInverse(mult.Left, out var denLeft):
                numerator = mult.Right;
                denominator = denLeft;
                return true;

            case Power when IsInverse(expr, out var denPow):
                numerator = new Constant(1);
                denominator = denPow;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Правило Лопиталя применимо только к 0/0 и ∞/∞. Если значение хотя бы одной
    /// части посчитать не удалось, считаем случай неопределённым и правило пробуем.
    /// </summary>
    private static bool IsIndeterminateQuotient(Expression numerator, Expression denominator,
                                                string variable, double point)
    {
        var vars = new Dictionary<string, double> { { variable, point } };

        double? Value(Expression e)
        {
            try
            {
                double v = EvaluateExpression(e, vars);
                return double.IsNaN(v) ? null : v;
            }
            catch { return null; }
        }

        double? f = Value(numerator), g = Value(denominator);
        if (f is null || g is null) return true;

        bool bothZero     = System.Math.Abs(f.Value) < 1e-9 && System.Math.Abs(g.Value) < 1e-9;
        bool bothInfinite = double.IsInfinity(f.Value) && double.IsInfinity(g.Value);
        return bothZero || bothInfinite;
    }

    /// <summary>
    /// Боковой подход с двумя масштабами ε. Рост значения на порядок при
    /// десятикратном приближении к точке — признак расходимости, и выдавать
    /// в этом случае «предел = 1e8» (значение в пробной точке) нельзя.
    /// </summary>
    private static string ProbeAroundPoint(Expression expr, string variable, double point)
    {
        double? At(double x)
        {
            try
            {
                double v = EvaluateExpression(expr, new Dictionary<string, double> { { variable, x } });
                return double.IsNaN(v) ? null : v;
            }
            catch { return null; }
        }

        const double epsilon = 1e-4;
        double? left = At(point - epsilon), right = At(point + epsilon);
        if (left is null || right is null)
            return "Предел требует символьного аналитического вычисления.";

        double? leftNear = At(point - (epsilon / 10)), rightNear = At(point + (epsilon / 10));
        bool Diverges(double? far, double? near) =>
            near is not null && far is not null &&
            System.Math.Abs(near.Value) > 5 * System.Math.Max(1.0, System.Math.Abs(far.Value));

        if (Diverges(left, leftNear) || Diverges(right, rightNear))
        {
            bool sameSign = leftNear is not null && rightNear is not null &&
                            System.Math.Sign(leftNear.Value) == System.Math.Sign(rightNear.Value);
            if (!sameSign)
                return "Предел не существует: слева и справа функция уходит в бесконечности разных знаков.";
            return leftNear!.Value > 0 ? "+∞  (функция неограниченно растёт)" : "-∞  (функция неограниченно убывает)";
        }

        if (System.Math.Abs(left.Value - right.Value) < 1e-3)
            return ((left.Value + right.Value) / 2).ToString("G6");

        return $"Левый предел = {left.Value:G6},  правый = {right.Value:G6} (различаются -> возможна разрывность)";
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
