using System.Globalization;
using System.Text.RegularExpressions;
using AI.ClassicMath.MatrixUtils.FindFraction;
using AI.Solvers.Math.Core.Integrations;
using AI.Solvers.Math.Core.Numerics;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

public static class IntegralSolver
{
    public static string IndefiniteIntegral(string expression, string variable = "x")
    {
        try
        {
            var expr = AdvancedMathExpression.Parse(expression);
            var integral = AdvancedIntegrationEngine.Integrate(expr, variable);
            integral = integral.Simplify();

            string result = FormatWithFractions(integral);
            return $"{result} + C";
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Наибольший знаменатель, при котором дробь читается лучше десятичной записи.
    /// </summary>
    private const int MaxReadableDenominator = 64;

    /// <summary>
    /// Заменяет десятичные литералы рациональными дробями.
    /// Поиск идёт по ЦЕЛОМУ литералу (границы \b), а не по подстроке: замена подстрок
    /// превращала 10.5 в «11/2», а 20.4 — в «22/5». Дробь берётся из
    /// <see cref="NumberConverter"/> и всегда скобкуется, иначе x^0.5 дало бы x^1/2,
    /// что разбирается обратно как (x^1)/2.
    /// </summary>
    private static string FormatWithFractions(Expression expr) =>
        // Границы: слева не цифра и не точка (иначе 10.5 матчится как «0.5»),
        // справа не цифра, не точка и не признак экспоненты (иначе 2.75573E-06
        // распадётся на дробь и хвост «E-06»). Буква справа допустима: ToString
        // печатает произведение как «0.5asin(x)», без знака умножения.
        Regex.Replace(expr.ToString(), @"(?<![\d.])\d+\.\d+(?![\d.eE])", m =>
        {
            if (!double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return m.Value;

            var analysis = NumberConverter.Analyze(value);
            if (analysis.Type is not (ConversionType.Terminating or ConversionType.Repeating))
                return m.Value;
            if (analysis.Denominator <= 1 || analysis.Denominator > MaxReadableDenominator)
                return m.Value;

            // Analyze умеет отдавать и π/4, и √2/2 — там Numerator/Denominator
            // относятся к множителю, а не к самому числу. Проверка возвратом отсекает такие случаи.
            double restored = (double)analysis.Numerator / (double)analysis.Denominator;
            if (System.Math.Abs(restored - value) > 1e-10 * System.Math.Max(1, System.Math.Abs(value)))
                return m.Value;

            return $"({analysis.Numerator}/{analysis.Denominator})";
        });

    // Определенный интеграл (метод трапеций)
    public static string DefiniteIntegral(string expression, string variable, double lowerBound, double upperBound)
    {
        try
        {
            var expr = AdvancedMathExpression.Parse(expression);

            // Пробуем символьное интегрирование
            try
            {
                var integral = AdvancedIntegrationEngine.Integrate(expr, variable);
                var vars = new Dictionary<string, double>();

                vars[variable] = upperBound;
                var upperValue = EvaluateExpression(integral, vars);

                vars[variable] = lowerBound;
                var lowerValue = EvaluateExpression(integral, vars);

                var result = upperValue - lowerValue;

                // Проверяем, что результат валидный
                if (!double.IsNaN(result) && !double.IsInfinity(result))
                    return $"{result:G6}";
            }
            catch
            {
                // Намеренно проглатываем: символьное интегрирование может потерпеть
                // неудачу для сложных подынтегральных выражений или из-за пропущенных
                // AST-узлов в ExpressionEvaluator. Падаем на численный метод (ниже).
            }
            var numericalResult = IntegrateNumerically(expr, variable, lowerBound, upperBound);
            return $"{numericalResult:G6}";

        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    // Численное интегрирование общей квадратурой зоны
    private static double IntegrateNumerically(Expression expr, string variable, double a, double b)
    {
        var vars = new Dictionary<string, double>();
        return Quadrature.Integrate(x =>
        {
            vars[variable] = x;
            return EvaluateExpression(expr, vars);
        }, a, b);
    }

    // Двойной интеграл (символьный)
    public static string DoubleIntegral(string expression, string var1, string var2)
    {
        try
        {
            var expr = AdvancedMathExpression.Parse(expression);

            var firstIntegral = AdvancedIntegrationEngine.Integrate(expr, var1);

            var secondIntegral = AdvancedIntegrationEngine.Integrate(firstIntegral, var2);

            return $"{secondIntegral} + C";
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    private static double EvaluateExpression(Expression expr, Dictionary<string, double> variables)
        => ExpressionEvaluator.Evaluate(expr, variables);
}

