using System.Text;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class AdvancedSolver
{
    #region Решение уравнений

    public static string SolveEquation(string equation)
    {
        try
        {
            // Квадратное: x^2 ± bx ± c = 0
            var match = System.Text.RegularExpressions.Regex.Match(
                equation,
                @"([a-z])\^2\s*([+\-])\s*([\d\.]+)\*?\1\s*([+\-])\s*([\d\.]+)\s*=\s*0");

            if (match.Success)
                return SolveQuadratic(match);

            // Линейное: ax + b = 0
            match = System.Text.RegularExpressions.Regex.Match(
                equation,
                @"([\d\.]+)?\*?([a-z])\s*([+\-])\s*([\d\.]+)\s*=\s*0");

            if (match.Success)
                return SolveLinear(match);

            return SolveNumerically(equation);
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    private static string SolveQuadratic(System.Text.RegularExpressions.Match m)
    {
        var variable    = m.Groups[1].Value;
        double b = double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
        double c = double.Parse(m.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (m.Groups[2].Value == "-") b = -b;
        if (m.Groups[4].Value == "-") c = -c;

        double discriminant = b * b - 4 * c;

        var sb = new StringBuilder();
        sb.AppendLine("=== СИМВОЛЬНОЕ РЕШЕНИЕ ===");
        sb.AppendLine();
        sb.AppendLine($"Квадратное уравнение: {variable}² {(b >= 0 ? "+" : "")}{b}{variable} {(c >= 0 ? "+" : "")}{c} = 0");
        sb.AppendLine($"Дискриминант: D = b² - 4ac = {discriminant:G6}");
        sb.AppendLine();

        if (discriminant > 0)
        {
            sb.AppendLine($"D > 0 -> два действительных корня:");
            sb.AppendLine($"  {variable}₁ = {(-b + System.Math.Sqrt(discriminant)) / 2:G10}");
            sb.AppendLine($"  {variable}₂ = {(-b - System.Math.Sqrt(discriminant)) / 2:G10}");
        }
        else if (System.Math.Abs(discriminant) < 1e-10)
        {
            sb.AppendLine($"D = 0 -> один кратный корень:");
            sb.AppendLine($"  {variable} = {-b / 2:G10}");
        }
        else
        {
            double real = -b / 2;
            double imag = System.Math.Sqrt(-discriminant) / 2;
            sb.AppendLine($"D < 0 -> два комплексных корня:");
            sb.AppendLine($"  {variable}₁ = {real:G6} + {imag:G6}i");
            sb.AppendLine($"  {variable}₂ = {real:G6} - {imag:G6}i");
        }

        return sb.ToString();
    }

    private static string SolveLinear(System.Text.RegularExpressions.Match m)
    {
        // Шаблон ax + b = 0  (или ax - b = 0). Группы:
        //   [1] = "a" (опционально, по умолчанию 1)
        //   [2] = переменная
        //   [3] = знак между ax и b ("+" или "-")
        //   [4] = модуль b
        // Чтобы получить b со знаком, нужно умножить на -1, если оператор "-".
        double a = m.Groups[1].Success && !string.IsNullOrEmpty(m.Groups[1].Value)
            ? double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 1.0;
        var variable = m.Groups[2].Value;
        double bAbs = double.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
        double b = m.Groups[3].Value == "-" ? -bAbs : bAbs;
        if (System.Math.Abs(a) < 1e-12)
            return "Линейное уравнение вырождено: коэффициент при переменной равен 0.";
        double x = -b / a;

        var sb = new StringBuilder();
        sb.AppendLine("=== СИМВОЛЬНОЕ РЕШЕНИЕ ===");
        sb.AppendLine();
        sb.AppendLine($"Линейное уравнение: {a}{variable} {(b >= 0 ? "+" : "")}{b} = 0");
        sb.AppendLine($"Решение: {variable} = -({b})/{a} = {x:G10}");
        return sb.ToString();
    }

    private static string SolveNumerically(string equation)
    {
        try
        {
            var parts = equation.Split('=');
            if (parts.Length != 2)
                return $"Неверный формат уравнения: {equation}\nОжидается: выражение = выражение";

            Expression leftExpr, rightExpr;
            try
            {
                leftExpr  = AdvancedMathExpression.Parse(parts[0].Trim());
                rightExpr = AdvancedMathExpression.Parse(parts[1].Trim());
            }
            catch (Exception ex)
            {
                return $"Не удалось распознать уравнение: {equation}\nОшибка парсинга: {ex.Message}";
            }

            var variables = new HashSet<string>();
            CollectVariables(leftExpr, variables);
            CollectVariables(rightExpr, variables);

            if (variables.Count == 0)
            {
                var leftVal  = EvaluateExpression(leftExpr,  new Dictionary<string, double>());
                var rightVal = EvaluateExpression(rightExpr, new Dictionary<string, double>());
                return System.Math.Abs(leftVal - rightVal) < 1e-10
                    ? $"Уравнение верно: {leftVal:G10} = {rightVal:G10}"
                    : $"Уравнение неверно: {leftVal:G10} ≠ {rightVal:G10}";
            }

            if (variables.Count > 1)
                return $"Уравнение содержит несколько переменных: {string.Join(", ", variables)}\n" +
                       "Решение систем уравнений пока не поддерживается";

            return NumericalEquationSolver.SolveNumerically(leftExpr, rightExpr, variables.First());
        }
        catch (Exception ex)
        {
            return $"Ошибка численного решения: {ex.Message}";
        }
    }

    #endregion
}
