using System.Text;
using System.Text.RegularExpressions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

/// <summary>
/// Система двух линейных ОДУ 1-го порядка с постоянными коэффициентами:
/// x' = a₁₁x + a₁₂y,  y' = a₂₁x + a₂₂y.
/// Решение строится по собственным значениям матрицы, а не по заранее
/// заготовленному тригонометрическому ответу: тот верен только для
/// x' = y, y' = -x, а для x' = y, y' = x решение экспоненциальное.
/// </summary>
public static partial class ODESolver
{
    #region Система ОДУ

    public static string SolveSystemODE(List<string> equations)
    {
        try
        {
            if (equations is not { Count: 2 })
                return "Поддерживаются только системы из двух уравнений вида\n" +
                       "  x' = a*x + b*y\n  y' = c*x + d*y";

            if (!TryParseEquation(equations[0], out string name1, out Expression rhs1) ||
                !TryParseEquation(equations[1], out string name2, out Expression rhs2))
                return "Не удалось разобрать систему. Ожидается форма:\n" +
                       "  x' = a*x + b*y\n  y' = c*x + d*y";

            if (name1 == name2)
                return $"Обе производные записаны для одной функции '{name1}'.";

            if (!TryLinearCoefficients(rhs1, name1, name2, out double a11, out double a12, out double free1) ||
                !TryLinearCoefficients(rhs2, name1, name2, out double a21, out double a22, out double free2))
                return "Правые части должны быть линейными по обеим функциям с постоянными коэффициентами.";

            if (System.Math.Abs(free1) > 1e-12 || System.Math.Abs(free2) > 1e-12)
                return "Неоднородная система (со свободными членами) пока не поддерживается.\n" +
                       "Сведите её к однородной сдвигом на положение равновесия.";

            return Describe(name1, name2, a11, a12, a21, a22);
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    private static bool TryParseEquation(string equation, out string funcName, out Expression rhs)
    {
        funcName = "";
        rhs = new Constant(0);

        var match = Regex.Match(equation.Trim(), @"^([a-z])'\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        funcName = match.Groups[1].Value;
        try
        {
            rhs = AdvancedMathExpression.Parse(match.Groups[2].Value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Раскладывает выражение на a·v1 + b·v2 + свободный член.</summary>
    private static bool TryLinearCoefficients(Expression expr, string v1, string v2,
                                              out double a, out double b, out double free)
    {
        a = 0; b = 0; free = 0;
        return Accumulate(expr, v1, v2, 1.0, ref a, ref b, ref free);
    }

    private static bool Accumulate(Expression expr, string v1, string v2, double scale,
                                   ref double a, ref double b, ref double free)
    {
        switch (expr)
        {
            case Constant c:
                free += scale * c.Value;
                return true;

            case Variable v when v.Name == v1:
                a += scale;
                return true;

            case Variable v when v.Name == v2:
                b += scale;
                return true;

            case Variable:
                return false;

            case Add add:
                return Accumulate(add.Left,  v1, v2, scale, ref a, ref b, ref free) &&
                       Accumulate(add.Right, v1, v2, scale, ref a, ref b, ref free);

            case Multiply m when m.Left is Constant cl:
                return Accumulate(m.Right, v1, v2, scale * cl.Value, ref a, ref b, ref free);

            case Multiply m when m.Right is Constant cr:
                return Accumulate(m.Left, v1, v2, scale * cr.Value, ref a, ref b, ref free);

            case Divide d when d.Denominator is Constant cd && System.Math.Abs(cd.Value) > 1e-12:
                return Accumulate(d.Numerator, v1, v2, scale / cd.Value, ref a, ref b, ref free);

            default:
                return false;
        }
    }

    private static string Describe(string x, string y, double a11, double a12, double a21, double a22)
    {
        double trace = a11 + a22;
        double det   = a11 * a22 - a12 * a21;
        double disc  = trace * trace - 4 * det;

        var sb = new StringBuilder();
        sb.AppendLine("=== СИСТЕМА ЛИНЕЙНЫХ ОДУ С ПОСТОЯННЫМИ КОЭФФИЦИЕНТАМИ ===");
        sb.AppendLine();
        sb.AppendLine($"{x}' = {N(a11)}*{x} + {N(a12)}*{y}");
        sb.AppendLine($"{y}' = {N(a21)}*{x} + {N(a22)}*{y}");
        sb.AppendLine();
        sb.AppendLine($"След: tr = {N(trace)},  определитель: det = {N(det)}");
        sb.AppendLine($"Характеристическое уравнение: λ² - {N(trace)}λ + {N(det)} = 0");
        sb.AppendLine($"Дискриминант: D = {N(disc)}");
        sb.AppendLine();

        if (disc > 1e-12)
        {
            double root = System.Math.Sqrt(disc);
            double l1 = (trace + root) / 2, l2 = (trace - root) / 2;
            var (v1x, v1y) = Eigenvector(a11, a12, a21, a22, l1);
            var (v2x, v2y) = Eigenvector(a11, a12, a21, a22, l2);

            sb.AppendLine($"D > 0: собственные значения λ₁ = {N(l1)}, λ₂ = {N(l2)}");
            sb.AppendLine($"Собственные векторы: v₁ = ({N(v1x)}, {N(v1y)}), v₂ = ({N(v2x)}, {N(v2y)})");
            sb.AppendLine();
            sb.AppendLine("ОТВЕТ:");
            sb.AppendLine($"  {x}(t) = {N(v1x)}*C1*exp({N(l1)}*t) + {N(v2x)}*C2*exp({N(l2)}*t)");
            sb.AppendLine($"  {y}(t) = {N(v1y)}*C1*exp({N(l1)}*t) + {N(v2y)}*C2*exp({N(l2)}*t)");
            return sb.ToString();
        }

        if (disc < -1e-12)
        {
            double alpha = trace / 2, beta = System.Math.Sqrt(-disc) / 2;
            sb.AppendLine($"D < 0: собственные значения λ = {N(alpha)} ± {N(beta)}i");
            sb.AppendLine();
            sb.AppendLine("ОТВЕТ:");

            // Комплексный собственный вектор w = p + i·q; вещественные решения —
            // e^(αt)[C1(p·cos βt - q·sin βt) + C2(p·sin βt + q·cos βt)].
            if (System.Math.Abs(a12) > 1e-12)
            {
                double px = a12, py = alpha - a11, qy = beta;
                sb.AppendLine($"  {x}(t) = exp({N(alpha)}*t)*({N(px)}*C1*cos({N(beta)}*t) + {N(px)}*C2*sin({N(beta)}*t))");
                sb.AppendLine($"  {y}(t) = exp({N(alpha)}*t)*(C1*({N(py)}*cos({N(beta)}*t) - {N(qy)}*sin({N(beta)}*t))" +
                              $" + C2*({N(py)}*sin({N(beta)}*t) + {N(qy)}*cos({N(beta)}*t)))");
            }
            else
            {
                double px = alpha - a22, qx = beta, py = a21;
                sb.AppendLine($"  {x}(t) = exp({N(alpha)}*t)*(C1*({N(px)}*cos({N(beta)}*t) - {N(qx)}*sin({N(beta)}*t))" +
                              $" + C2*({N(px)}*sin({N(beta)}*t) + {N(qx)}*cos({N(beta)}*t)))");
                sb.AppendLine($"  {y}(t) = exp({N(alpha)}*t)*({N(py)}*C1*cos({N(beta)}*t) + {N(py)}*C2*sin({N(beta)}*t))");
            }
            return sb.ToString();
        }

        double lambda = trace / 2;
        sb.AppendLine($"D = 0: кратное собственное значение λ = {N(lambda)}");
        sb.AppendLine();
        sb.AppendLine("ОТВЕТ:");

        if (System.Math.Abs(a12) < 1e-12 && System.Math.Abs(a21) < 1e-12)
        {
            // Матрица скалярная: уравнения независимы
            sb.AppendLine($"  {x}(t) = C1*exp({N(lambda)}*t)");
            sb.AppendLine($"  {y}(t) = C2*exp({N(lambda)}*t)");
        }
        else if (System.Math.Abs(a12) > 1e-12)
        {
            // Жорданова клетка: v = (a12, λ-a11), присоединённый вектор w = (0, 1)
            sb.AppendLine($"  {x}(t) = exp({N(lambda)}*t)*{N(a12)}*(C1 + C2*t)");
            sb.AppendLine($"  {y}(t) = exp({N(lambda)}*t)*({N(lambda - a11)}*(C1 + C2*t) + C2)");
        }
        else
        {
            // v = (λ-a22, a21), присоединённый вектор w = (1, 0)
            sb.AppendLine($"  {x}(t) = exp({N(lambda)}*t)*({N(lambda - a22)}*(C1 + C2*t) + C2)");
            sb.AppendLine($"  {y}(t) = exp({N(lambda)}*t)*{N(a21)}*(C1 + C2*t)");
        }
        return sb.ToString();
    }

    /// <summary>Собственный вектор матрицы 2×2 для собственного значения λ.</summary>
    private static (double x, double y) Eigenvector(double a11, double a12, double a21, double a22, double lambda)
    {
        if (System.Math.Abs(a12) > 1e-12) return (a12, lambda - a11);
        if (System.Math.Abs(a21) > 1e-12) return (lambda - a22, a21);
        // Диагональная матрица: собственные векторы — орты
        return System.Math.Abs(lambda - a11) < System.Math.Abs(lambda - a22) ? (1, 0) : (0, 1);
    }

    private static string N(double value) =>
        value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);

    #endregion
}
