using System.Numerics;
using System.Text;
using AI.ClassicMath.Calculator.Libs.Algebra;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class AdvancedSolver
{
    #region Решение уравнений

    /// <summary>Наибольшая степень, для которой в EquationLib есть точная формула (Феррари).</summary>
    private const int MaxAlgebraicDegree = 4;

    /// <summary>
    /// Разбирает уравнение через AST и решает: многочлены степени 1..4 — точно
    /// (EquationLib: линейное, квадратное, Кардано, Феррари), остальное — численно.
    /// </summary>
    public static string SolveEquation(string equation)
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
                return CheckIdentity(leftExpr, rightExpr);

            if (variables.Count > 1)
                return $"Уравнение содержит несколько переменных: {string.Join(", ", variables)}\n" +
                       "Решение систем уравнений пока не поддерживается";

            string variable = variables.First();
            var difference = new Add(leftExpr, new Multiply(new Constant(-1), rightExpr)).Simplify();

            if (PolynomialCoefficients.TryExtract(difference, variable, MaxAlgebraicDegree, out var coefficients))
            {
                int degree = PolynomialCoefficients.Degree(coefficients);
                if (degree >= 1)
                    return SolvePolynomial(coefficients, degree, variable, leftExpr, rightExpr);

                return System.Math.Abs(coefficients[0]) < 1e-12
                    ? "Уравнение выполняется при любом значении переменной."
                    : "Уравнение не имеет решений: после приведения осталась ненулевая константа.";
            }

            return NumericalEquationSolver.SolveNumerically(leftExpr, rightExpr, variable);
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    private static string CheckIdentity(Expression left, Expression right)
    {
        var noVariables = new Dictionary<string, double>();
        double leftValue  = EvaluateExpression(left,  noVariables);
        double rightValue = EvaluateExpression(right, noVariables);

        return System.Math.Abs(leftValue - rightValue) < 1e-10
            ? $"Уравнение верно: {leftValue:G10} = {rightValue:G10}"
            : $"Уравнение неверно: {leftValue:G10} ≠ {rightValue:G10}";
    }

    private static string SolvePolynomial(double[] c, int degree, string variable,
                                          Expression left, Expression right)
    {
        Complex[] roots = degree switch
        {
            1 => [LinearEquationSolver.Solve(c[1], c[0])],
            2 => QuadraticEquationSolver.Solve(c[2], c[1], c[0]).ToArray(),
            3 => CubicEquationSolver.Solve(c[3], c[2], c[1], c[0]).ToArray(),
            _ => QuarticEquationSolver.Solve(c[4], c[3], c[2], c[1], c[0]).ToArray()
        };

        var sb = new StringBuilder();
        sb.AppendLine("=== СИМВОЛЬНОЕ РЕШЕНИЕ ===");
        sb.AppendLine();
        sb.AppendLine($"Уравнение: {left} = {right}");
        sb.AppendLine($"Приведённый вид: {FormatPolynomial(c, degree, variable)} = 0");
        sb.AppendLine($"Степень: {degree}");
        if (degree == 2)
            sb.AppendLine($"Дискриминант: D = b² - 4ac = {c[1] * c[1] - 4 * c[2] * c[0]:G6}");
        sb.AppendLine();
        sb.AppendLine($"КОРНИ ({roots.Length}):");

        for (int i = 0; i < roots.Length; i++)
            sb.AppendLine($"  {variable}_{i + 1} = {FormatComplex(roots[i])}");

        return sb.ToString();
    }

    private static string FormatComplex(Complex value)
    {
        if (System.Math.Abs(value.Imaginary) < 1e-10)
            return $"{value.Real:G10}";

        string sign = value.Imaginary >= 0 ? "+" : "-";
        return $"{value.Real:G6} {sign} {System.Math.Abs(value.Imaginary):G6}i";
    }

    private static string FormatPolynomial(double[] c, int degree, string variable)
    {
        var sb = new StringBuilder();
        for (int k = degree; k >= 0; k--)
        {
            double value = c[k];
            if (System.Math.Abs(value) < 1e-12) continue;

            if (sb.Length > 0) sb.Append(value > 0 ? " + " : " - ");
            else if (value < 0) sb.Append('-');

            double magnitude = System.Math.Abs(value);
            string coefficient = k > 0 && System.Math.Abs(magnitude - 1) < 1e-12 ? "" : $"{magnitude:G6}";
            sb.Append(k switch
            {
                0 => coefficient,
                1 => $"{coefficient}{variable}",
                _ => $"{coefficient}{variable}^{k}"
            });
        }
        return sb.Length == 0 ? "0" : sb.ToString();
    }

    #endregion
}
