using System.Text;
using AI.HighLevelFunctions;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class AdvancedSolver
{
    #region Ряд Тейлора

    public static string TaylorSeries(string expression, string variable, string point, int terms = 10)
    {
        try
        {
            var expr = AdvancedMathExpression.Parse(expression);
            var center = point == "0" ? 0.0 : double.Parse(point, System.Globalization.CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            var vars = new Dictionary<string, double> { { variable, center } };

            double f0 = EvaluateExpression(expr, vars);

            if (System.Math.Abs(f0) > 1e-10)
                sb.Append($"{f0:G6}");

            var currentExpr = expr;
            bool firstTerm = System.Math.Abs(f0) < 1e-10;

            for (int n = 1; n < terms; n++)
            {
                currentExpr = currentExpr.Derivative(variable).Simplify();
                double derivValue = EvaluateExpression(currentExpr, vars);
                double coeff = derivValue / FunctionsForEachElements.Factorial(n);

                if (System.Math.Abs(coeff) > 1e-10)
                {
                    if (firstTerm)
                    {
                        // Сохраняем знак первого ненулевого члена: для f(x)=-cos(x) при центре 0
                        // первый ненулевой коэффициент — отрицательный, и его нельзя терять.
                        string firstSign = coeff < 0 ? "-" : "";
                        sb.Append(firstSign);
                        AppendTerm(sb, System.Math.Abs(coeff), variable, center, n, true);
                        firstTerm = false;
                    }
                    else
                    {
                        string sign = coeff > 0 ? " + " : " - ";
                        AppendTerm(sb, System.Math.Abs(coeff), variable, center, n, false, sign);
                    }
                }
            }

            sb.Append(" + ...");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return TryKnownSeries(expression, ex);
        }
    }

    private static void AppendTerm(StringBuilder sb, double coeff, string variable, double center, int n,
                                   bool first, string sign = "")
    {
        sb.Append(sign);
        if (center == 0)
        {
            if (n == 1) sb.Append($"{coeff:G6}*{variable}");
            else        sb.Append($"{coeff:G6}*{variable}^{n}");
        }
        else
        {
            if (n == 1) sb.Append($"{coeff:G6}*({variable}-{center})");
            else        sb.Append($"{coeff:G6}*({variable}-{center})^{n}");
        }
    }

    private static string TryKnownSeries(string expression, Exception ex)
    {
        var exprLower = expression.ToLower().Trim();
        return exprLower switch
        {
            "sin(x)" => "x - x³/6 + x⁵/120 - x⁷/5040 + x⁹/362880 - x¹¹/39916800 + ...",
            "cos(x)" => "1 - x²/2 + x⁴/24 - x⁶/720 + x⁸/40320 - x¹⁰/3628800 + ...",
            "exp(x)" or "e^x" => "1 + x + x²/2 + x³/6 + x⁴/24 + x⁵/120 + x⁶/720 + x⁷/5040 + x⁸/40320 + x⁹/362880 + ...",
            "ln(1+x)" => "x - x²/2 + x³/3 - x⁴/4 + x⁵/5 - x⁶/6 + x⁷/7 - x⁸/8 + ...",
            _ => $"Ошибка: {ex.Message}"
        };
    }

    #endregion
}
