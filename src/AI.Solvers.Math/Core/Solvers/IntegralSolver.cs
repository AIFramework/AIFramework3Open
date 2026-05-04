using AI.Solvers.Math.Core.Integrations;
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

    // Форматирование с дробями вместо десятичных дробей
    private static string FormatWithFractions(Expression expr)
    {
        string str = expr.ToString();

        // Заменяем распространенные десятичные дроби на обычные
        var fractions = new Dictionary<string, string>
        {
            { "0.3333333333333333", "1/3" },
            { "0.333333333", "1/3" },
            { "0.6666666666666666", "2/3" },
            { "0.666666667", "2/3" },
            { "0.5", "1/2" },
            { "0.25", "1/4" },
            { "0.75", "3/4" },
            { "0.2", "1/5" },
            { "0.4", "2/5" },
            { "0.6", "3/5" },
            { "0.8", "4/5" },
            { "0.1666666666666667", "1/6" },
            { "0.166666667", "1/6" },
            { "0.8333333333333333", "5/6" },
            { "0.833333333", "5/6" }
        };

        foreach (var (decimalStr, fractionStr) in fractions)
        {
            str = str.Replace(decimalStr, fractionStr);
        }

        return str;
    }

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
            var numericalResult = TrapezoidalRule(expr, variable, lowerBound, upperBound, 1000);
            return $"{numericalResult:G6}";

        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    // Метод трапеций для численного интегрирования
    private static double TrapezoidalRule(Expression expr, string variable, double a, double b, int n)
    {
        if (n <= 0)
            throw new ArgumentOutOfRangeException(nameof(n), "Число подынтервалов n должно быть положительным.");
        if (a == b) return 0.0;
        double h = (b - a) / n;
        double sum = 0.0;
        var vars = new Dictionary<string, double>();

        // f(a) + f(b)
        vars[variable] = a;
        sum += EvaluateExpression(expr, vars);

        vars[variable] = b;
        sum += EvaluateExpression(expr, vars);

        // 2 * [f(x1) + f(x2) + ... + f(x_{n-1})]
        for (int i = 1; i < n; i++)
        {
            vars[variable] = a + i * h;
            sum += 2 * EvaluateExpression(expr, vars);
        }

        return h / 2 * sum;
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

