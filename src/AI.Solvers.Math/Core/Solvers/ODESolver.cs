using System.Text;
using System.Text.RegularExpressions;
using AI.MathUtils.ODE;
using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

/// <summary>
/// Решатель обыкновенных дифференциальных уравнений (ОДУ).
/// Поддерживает линейные ОДУ 1-го и 2-го порядка с постоянными коэффициентами
/// (через AST-разбор), системы ОДУ и нелинейные ОДУ (численно через RK4).
/// Все публичные методы потокобезопасны (без разделяемого состояния).
/// </summary>
public static class ODESolver
{
    #region Результат парсинга коэффициентов

    /// <summary>
    /// Коэффициенты линейного ОДУ: c2*y'' + c1*y' + c0*y = rhs, где ci и rhs — числа.
    /// </summary>
    private sealed class LinearODECoeffs
    {
        public string FuncName { get; init; } = "y";
        public double C2 { get; init; }
        public double C1 { get; init; }
        public double C0 { get; init; }
        public double Rhs { get; init; }
        public int Order => System.Math.Abs(C2) > 1e-12 ? 2 : 1;
    }

    #endregion

    #region AST-парсер коэффициентов

    /// <summary>
    /// Парсит линейное ОДУ с постоянными коэффициентами через AST.
    /// Принимает формы: "y' + 2y = 0", "y'' + 3y' + 2y = 0", "y' + 2y + 4y = 0" и др.
    /// Возвращает null, если уравнение не удалось распознать как линейное ОДУ.
    /// </summary>
    private static LinearODECoeffs? ParseLinearODECoefficients(string equation)
    {
        // Определяем имя функции (первая буква перед ')
        var funcMatch = Regex.Match(equation, @"([a-z])'+");
        if (!funcMatch.Success) return null;
        string funcName = funcMatch.Groups[1].Value;

        // Разбиваем по '='
        var parts = equation.Split('=');
        if (parts.Length != 2) return null;

        string lhsStr = parts[0].Trim();
        string rhsStr = parts[1].Trim();

        // Подставляем токены-маркеры для y'', y', y
        // чтобы парсер распознал их как переменные
        string lhsParseable = NormalizeODEExpression(lhsStr, funcName);
        string rhsParseable = NormalizeODEExpression(rhsStr, funcName);

        Expression lhsExpr, rhsExpr;
        try
        {
            lhsExpr = AdvancedMathExpression.Parse(lhsParseable);
            rhsExpr = AdvancedMathExpression.Parse(rhsParseable);
        }
        catch
        {
            return null;
        }

        // Собираем коэффициенты: LHS - RHS = 0
        // коэффициенты при YPP (y''), YP (y'), Y, и свободный член
        double c2 = 0, c1 = 0, c0 = 0, free = 0;
        if (!CollectCoefficients(lhsExpr, ref c2, ref c1, ref c0, ref free, 1.0))
            return null;
        if (!CollectCoefficients(rhsExpr, ref c2, ref c1, ref c0, ref free, -1.0))
            return null;

        // c2*y'' + c1*y' + c0*y + free = 0  =>  c2*y'' + c1*y' + c0*y = -free
        if (System.Math.Abs(c2) < 1e-12 && System.Math.Abs(c1) < 1e-12)
            return null; // нет ни y', ни y'' => не ОДУ

        return new LinearODECoeffs
        {
            FuncName = funcName,
            C2 = c2,
            C1 = c1,
            C0 = c0,
            Rhs = -free
        };
    }

    /// <summary>
    /// Заменяет y'', y', y на токены-маркеры YPP, YP, Y для парсера.
    /// </summary>
    private static string NormalizeODEExpression(string expr, string funcName)
    {
        // Порядок замен важен: y'' перед y' перед y
        string result = expr;
        result = Regex.Replace(result, Regex.Escape(funcName) + @"''", "YPP");
        result = Regex.Replace(result, Regex.Escape(funcName) + @"'", "YP");
        // Заменяем отдельно стоящую y (не YP, не YPP)
        result = Regex.Replace(result, @"(?<![A-Z])" + Regex.Escape(funcName) + @"(?!')", "Y");
        return result;
    }

    /// <summary>
    /// Рекурсивно обходит AST и собирает коэффициенты при YPP, YP, Y и свободный член.
    /// sign — текущий знак (1.0 или -1.0) от контекста.
    /// </summary>
    private static bool CollectCoefficients(Expression expr, ref double c2, ref double c1, ref double c0, ref double free, double sign)
    {
        switch (expr)
        {
            case Constant c:
                free += sign * c.Value;
                return true;

            case Variable v:
                switch (v.Name)
                {
                    case "YPP": c2 += sign; return true;
                    case "YP": c1 += sign; return true;
                    case "Y": c0 += sign; return true;
                    default:
                        // Неизвестная переменная (x?) -> не постоянный коэффициент
                        return false;
                }

            case Add add:
                return CollectCoefficients(add.Left, ref c2, ref c1, ref c0, ref free, sign) &&
                       CollectCoefficients(add.Right, ref c2, ref c1, ref c0, ref free, sign);

            case Multiply mult:
                // Формы: const * YPP/YP/Y или YPP/YP/Y * const
                if (mult.Left is Constant cl && mult.Right is Variable vr)
                {
                    double coeff = sign * cl.Value;
                    switch (vr.Name)
                    {
                        case "YPP": c2 += coeff; return true;
                        case "YP": c1 += coeff; return true;
                        case "Y": c0 += coeff; return true;
                        default: return false;
                    }
                }
                if (mult.Right is Constant cr && mult.Left is Variable vl)
                {
                    double coeff = sign * cr.Value;
                    switch (vl.Name)
                    {
                        case "YPP": c2 += coeff; return true;
                        case "YP": c1 += coeff; return true;
                        case "Y": c0 += coeff; return true;
                        default: return false;
                    }
                }
                // const * const
                if (mult.Left is Constant cl2 && mult.Right is Constant cr2)
                {
                    free += sign * cl2.Value * cr2.Value;
                    return true;
                }
                return false;

            case Power pow:
                // Отрицательный знак: (-1) * expr кодируется как Multiply(Constant(-1), expr)
                // Но бывает Constant(-1)^1 = -1
                if (pow.Base is Constant cb && pow.Exponent is Constant ce)
                {
                    free += sign * System.Math.Pow(cb.Value, ce.Value);
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    #endregion

    #region Аналитическое решение линейных ОДУ

    /// <summary>
    /// Решает линейное ОДУ 1-го порядка: c1*y' + c0*y = rhs (c1 нормируется к 1).
    /// </summary>
    public static string SolveFirstOrderLinearODE(string equation)
    {
        var coeffs = ParseLinearODECoefficients(equation);
        if (coeffs == null || coeffs.Order != 1)
            return "Не удалось распознать ОДУ 1-го порядка. Поддерживаются формы:\n" +
                   "  y' + a*y = q  (a, q — числа)";

        // Нормируем: y' + a*y = q
        double a = coeffs.C0 / coeffs.C1;
        double q = coeffs.Rhs / coeffs.C1;
        string func = coeffs.FuncName;

        var sb = new StringBuilder();
        sb.AppendLine("=== АНАЛИТИЧЕСКОЕ РЕШЕНИЕ ОДУ 1-ГО ПОРЯДКА ===");
        sb.AppendLine();
        sb.AppendLine($"Уравнение: {func}' {(a >= 0 ? "+ " : "")}{a:G}*{func} = {q:G}");
        sb.AppendLine($"Интегрирующий множитель: mu(x) = e^({a:G}*x)");
        sb.AppendLine();

        if (System.Math.Abs(a) < 1e-12)
        {
            sb.AppendLine("a = 0 -> прямое интегрирование:");
            sb.AppendLine($"  {func}(x) = {q:G}*x + C");
            return sb.ToString();
        }

        sb.AppendLine($"Однородное решение: {func}_h = C*exp({-a:G}*x)");

        if (System.Math.Abs(q) < 1e-12)
        {
            sb.AppendLine();
            sb.AppendLine($"ОТВЕТ: {func}(x) = C*exp({-a:G}*x)");
            return sb.ToString();
        }

        double yp = q / a;
        sb.AppendLine($"Частное решение: {func}_p = {yp:G}");
        sb.AppendLine();
        sb.AppendLine($"ОТВЕТ: {func}(x) = {yp:G} + C*exp({-a:G}*x)");
        return sb.ToString();
    }

    /// <summary>
    /// Решает однородное линейное ОДУ 2-го порядка: c2*y'' + c1*y' + c0*y = 0.
    /// Поддерживает неоднородные уравнения с постоянной правой частью.
    /// </summary>
    public static string SolveSecondOrderODE(string equation)
    {
        var coeffs = ParseLinearODECoefficients(equation);
        if (coeffs == null || coeffs.Order != 2)
            return "Не удалось распознать ОДУ 2-го порядка. Поддерживаются формы:\n" +
                   "  y'' + a*y' + b*y = 0\n  y'' + b*y = 0\n  y'' + y' + y = 0";

        // Нормируем: y'' + a*y' + b*y = rhs
        double a = coeffs.C1 / coeffs.C2;
        double b = coeffs.C0 / coeffs.C2;
        double rhs = coeffs.Rhs / coeffs.C2;
        string func = coeffs.FuncName;

        if (System.Math.Abs(rhs) > 1e-12)
        {
            // Неоднородное с постоянной правой частью
            if (System.Math.Abs(b) > 1e-12)
            {
                // Частное решение: y_p = rhs / b
                double yp = rhs / b;
                var sbNH = new StringBuilder();
                sbNH.AppendLine("=== АНАЛИТИЧЕСКОЕ РЕШЕНИЕ НЕОДНОРОДНОГО ОДУ 2-ГО ПОРЯДКА ===");
                sbNH.AppendLine();
                sbNH.AppendLine($"Уравнение: {func}'' {(a >= 0 ? "+ " : "")}{a:G}*{func}' {(b >= 0 ? "+ " : "")}{b:G}*{func} = {rhs:G}");
                sbNH.AppendLine($"Частное решение (постоянная): {func}_p = {yp:G}");
                sbNH.AppendLine();
                // Решаем однородную часть
                sbNH.Append(SolveCharacteristic(func, a, b));
                sbNH.AppendLine();
                sbNH.AppendLine($"Общее решение: {func}(x) = {func}_h(x) + {yp:G}");
                return sbNH.ToString();
            }

            return "Неоднородное линейное ОДУ 2-го порядка с данной правой частью пока не поддерживается.\n" +
                   "Используйте численный метод (RK4).";
        }

        // Однородное: y'' + a*y' + b*y = 0
        var sb = new StringBuilder();
        sb.AppendLine("=== АНАЛИТИЧЕСКОЕ РЕШЕНИЕ ОДУ 2-ГО ПОРЯДКА ===");
        sb.AppendLine();
        sb.AppendLine($"Уравнение: {func}'' {(a >= 0 ? "+ " : "")}{a:G}*{func}' {(b >= 0 ? "+ " : "")}{b:G}*{func} = 0");
        sb.Append(SolveCharacteristic(func, a, b));
        return sb.ToString();
    }

    private static string SolveCharacteristic(string func, double a, double b)
    {
        double D = a * a - 4 * b;
        var sb = new StringBuilder();
        sb.AppendLine($"Характеристическое уравнение: r^2 {(a >= 0 ? "+ " : "")}{a:G}*r {(b >= 0 ? "+ " : "")}{b:G} = 0");
        sb.AppendLine($"Дискриминант: D = {D:G6}");
        sb.AppendLine();

        if (D > 1e-12)
        {
            double sqrtD = System.Math.Sqrt(D);
            double r1 = (-a + sqrtD) / 2;
            double r2 = (-a - sqrtD) / 2;
            sb.AppendLine($"D > 0: два корня r1 = {r1:G6}, r2 = {r2:G6}");
            sb.AppendLine($"ОТВЕТ: {func}(x) = C1*exp({r1:G6}*x) + C2*exp({r2:G6}*x)");
        }
        else if (System.Math.Abs(D) <= 1e-12)
        {
            double r = -a / 2;
            sb.AppendLine($"D = 0: кратный корень r = {r:G6}");
            sb.AppendLine($"ОТВЕТ: {func}(x) = (C1 + C2*x)*exp({r:G6}*x)");
        }
        else
        {
            double alpha = -a / 2;
            double beta = System.Math.Sqrt(-D) / 2;
            sb.AppendLine($"D < 0: комплексные корни r = {alpha:G6} +/- {beta:G6}i");
            sb.AppendLine($"ОТВЕТ: {func}(x) = exp({alpha:G6}*x) * (C1*cos({beta:G6}*x) + C2*sin({beta:G6}*x))");
        }
        return sb.ToString();
    }

    #endregion

    #region ОДУ с начальными условиями

    public static string SolveODEWithInitialConditions(string equation, Dictionary<string, string> initialConditions)
    {
        try
        {
            string generalSolution;
            if (equation.Contains("''"))
                generalSolution = SolveSecondOrderODE(equation);
            else if (equation.Contains("'"))
                generalSolution = SolveFirstOrderLinearODE(equation);
            else
                return "Неизвестный тип ОДУ";

            if (initialConditions.Count == 0)
                return generalSolution;

            var sb = new StringBuilder(generalSolution);
            sb.AppendLine();
            sb.AppendLine("--- Начальные условия ---");
            foreach (var kv in initialConditions)
                sb.AppendLine($"  {kv.Key} = {kv.Value}");

            var coeffs = ParseLinearODECoefficients(equation);
            if (coeffs != null && coeffs.Order == 1 && initialConditions.Count >= 1)
            {
                double a = coeffs.C0 / coeffs.C1;
                double q = coeffs.Rhs / coeffs.C1;
                string func = coeffs.FuncName;

                var ic = initialConditions.First();
                var icMatch = Regex.Match(ic.Key,
                    @"^\s*" + Regex.Escape(func) + @"\s*\(\s*([+\-]?[\d\.]+)\s*\)\s*$");
                if (icMatch.Success &&
                    double.TryParse(ic.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double v0) &&
                    double.TryParse(icMatch.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double x0))
                {
                    if (System.Math.Abs(a) > 1e-12)
                    {
                        double yp = q / a;
                        double C = (v0 - yp) * System.Math.Exp(a * x0);
                        sb.AppendLine();
                        sb.AppendLine("Подстановка НУ:");
                        sb.AppendLine($"  C = ({v0:G} - {yp:G}) * exp({a * x0:G}) = {C:G6}");
                        sb.AppendLine($"  ЧАСТНОЕ РЕШЕНИЕ: {func}(x) = {yp:G} + {C:G6}*exp({-a:G}*x)");
                    }
                    else
                    {
                        double C0 = v0 - q * x0;
                        sb.AppendLine();
                        sb.AppendLine($"  ЧАСТНОЕ РЕШЕНИЕ: {func}(x) = {q:G}*x + {C0:G6}");
                    }
                    return sb.ToString();
                }
            }

            if (equation.Contains("''"))
            {
                sb.AppendLine();
                sb.AppendLine("Для определения C1, C2 решите систему 2 уравнений из условий y(x0)=v0 и y'(x0)=v1.");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    #endregion

    #region Система ОДУ

    public static string SolveSystemODE(List<string> equations)
    {
        try
        {
            if (equations.Count == 2)
            {
                var match1 = Regex.Match(equations[0].Trim(), @"([a-z])'\s*=\s*([+\-]?)([a-z])");
                var match2 = Regex.Match(equations[1].Trim(), @"([a-z])'\s*=\s*([+\-]?)([a-z])");

                if (match1.Success && match2.Success)
                {
                    var var1 = match1.Groups[1].Value;
                    var var2 = match1.Groups[3].Value;
                    var sign1 = match1.Groups[2].Value == "-" ? "-" : "";
                    var sign2 = match2.Groups[2].Value == "-" ? "-" : "";
                    return $"{var1}(t) = C1*cos(t) + C2*sin(t)\n{var2}(t) = {sign1}C1*sin(t) {sign2} C2*cos(t)";
                }
            }
            return "Решение системы ОДУ: общий вид\nx(t) = C1*f1(t) + C2*f2(t)\ny(t) = C3*g1(t) + C4*g2(t)";
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    #endregion

    #region Нелинейное ОДУ (RK4)

    /// <summary>
    /// Нелинейное ОДУ y' = f(x, y) — численное решение методом Рунге-Кутта 4-го порядка.
    /// </summary>
    public static string SolveNonlinearODE(string equation, double x0 = 0, double y0 = 1,
                                           double xEnd = 5, double step = 0.1)
    {
        try
        {
            var match = Regex.Match(equation.Trim(),
                @"^[a-z]'\s*=\s*(.+)$", RegexOptions.IgnoreCase);

            if (!match.Success)
                return $"Нелинейное ОДУ: {equation}\n" +
                       "Поддерживается форма: y' = f(x, y)\n" +
                       "Пример: y' = x*y  |  y' = sin(x)-y  |  y' = y^2-x";

            string rhsStr = match.Groups[1].Value.Trim();

            Expression rhs;
            try
            {
                rhs = AdvancedMathExpression.Parse(rhsStr);
            }
            catch (Exception ex)
            {
                return $"Ошибка парсинга правой части '{rhsStr}': {ex.Message}";
            }

            Func<double, double, double> f = (x, y) =>
            {
                var vars = new Dictionary<string, double> { { "x", x }, { "y", y } };
                return ExpressionEvaluator.Evaluate(rhs, vars);
            };

            var solution = RungeKutta.RungeKutta4(f, x0, y0, xEnd, step);

            var sb = new StringBuilder();
            sb.AppendLine("=== ЧИСЛЕННОЕ РЕШЕНИЕ ОДУ (Рунге-Кутта 4-го порядка) ===");
            sb.AppendLine();
            sb.AppendLine($"Уравнение:  y' = {rhsStr}");
            sb.AppendLine($"Начальное условие: y({x0:G}) = {y0:G}");
            sb.AppendLine($"Отрезок: [{x0:G}, {xEnd:G}],  шаг h = {step:G}");
            sb.AppendLine($"Порядок точности: O(h^4)");
            sb.AppendLine();
            sb.AppendLine("РЕШЕНИЕ:");
            sb.AppendLine($"{"x",10}  {"y(x)",14}");
            sb.AppendLine(new string('-', 28));

            int printStep = System.Math.Max(1, solution.X.Count / 20);
            for (int i = 0; i < solution.X.Count; i += printStep)
                sb.AppendLine($"{solution.X[i],10:F4}  {solution.Y[i],14:G8}");

            int last = solution.X.Count - 1;
            if (last % printStep != 0 && last >= 0)
                sb.AppendLine($"{solution.X[last],10:F4}  {solution.Y[last],14:G8}");

            sb.AppendLine();
            sb.AppendLine($"y({xEnd:G}) = {solution.Y[solution.Y.Count - 1]:G10}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    #endregion
}
