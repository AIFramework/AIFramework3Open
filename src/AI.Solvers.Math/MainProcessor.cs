using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Parsers;
using AI.Solvers.Math.Core.Solvers;

namespace AI.Solvers.Math;

/// <summary>
/// Главная точка входа для обработки математических команд в стиле "фрактального парсинга".
/// <para>
/// <b>Потокобезопасность:</b> экземпляр класса не содержит разделяемого мутабельного состояния.
/// Все вычисления выполняются через локальные переменные и статические методы без побочных эффектов.
/// Парсеры создаются на каждый вызов, таблицы (LaplaceTable и др.) являются <c>static readonly</c>.
/// Класс безопасен для одновременного использования из нескольких потоков.
/// </para>
/// </summary>
public class MainFractalMathProcessor
{
    public string ProcessFractalMathCommand(string input)
    {
        // Парсим команду
        var command = FractalMathStyleParser.Parse(input);

        string result = command.Type switch
        {
            // Интегрирование
            CommandType.IndefiniteIntegral =>
                IntegralSolver.IndefiniteIntegral(command.Expression, command.Variable),

            CommandType.DefiniteIntegral =>
                IntegralSolver.DefiniteIntegral(
                    command.Expression,
                    command.Variable,
                    command.LowerBound ?? 0,
                    command.UpperBound ?? 1),

            CommandType.DoubleIntegral =>
                IntegralSolver.DoubleIntegral(command.Expression, command.Variable, command.Variable2),

            // Дифференцирование
            CommandType.FirstDerivative =>
                DerivativeSolver.FirstDerivative(command.Expression, command.Variable),

            CommandType.SecondDerivative =>
                DerivativeSolver.NthDerivative(command.Expression, command.Variable, 2),

            CommandType.NthDerivative =>
                DerivativeSolver.NthDerivative(command.Expression, command.Variable, command.Order),

            CommandType.PartialDerivative =>
                DerivativeSolver.PartialDerivative(command.Expression, command.Variable),

            // Дифференциальные уравнения
            CommandType.ODE => ProcessODE(command.Expression),

            CommandType.ODEWithInitialConditions =>
                ODESolver.SolveODEWithInitialConditions(command.Expression, command.InitialConditions),

            CommandType.SystemODE =>
                ODESolver.SolveSystemODE(command.Equations),

            CommandType.PDE => ProcessPDE(command.Expression),

            // Дополнительные функции
            CommandType.Limit =>
                AdvancedSolver.ComputeLimit(command.Expression, command.Variable, command.LimitPoint),

            CommandType.TaylorSeries =>
                AdvancedSolver.TaylorSeries(command.Expression, command.Variable, command.LimitPoint),

            CommandType.LaplaceTransform =>
                AdvancedSolver.LaplaceTransform(command.Expression, command.Variable),

            CommandType.LaplaceTable =>
                AdvancedSolver.ShowLaplaceTable(),

            CommandType.FourierTransform =>
                AdvancedSolver.FourierTransform(command.Expression, command.Variable),

            CommandType.Solve =>
                AdvancedSolver.SolveEquation(command.Expression),

            _ => string.IsNullOrEmpty(command.Expression)
                    ? "Команда не поддерживается. Используйте help для списка команд."
                    : command.Expression
        };

        return result;
    }

    protected string ProcessODE(string equation)
    {
        if (equation.Contains("''"))
        {
            string result = ODESolver.SolveSecondOrderODE(equation);
            if (!result.StartsWith("Не удалось"))
                return result;
        }

        if (equation.Contains("'"))
        {
            string result = ODESolver.SolveFirstOrderLinearODE(equation);
            if (!result.StartsWith("Не удалось"))
                return result;

            // Аналитика не сработала — пробуем численный метод (RK4)
            return ODESolver.SolveNonlinearODE(equation);
        }

        return "Неизвестный тип ОДУ";
    }

    protected string ProcessPDE(string equation)
    {
        var eqLower = equation.ToLower().Replace(" ", "");

        // Helper: содержится ли отдельный токен u_x (а не u_xx).
        // (^|[+\-*=(]) — начало строки ИЛИ разделитель слева;
        // (?!x) — отрицательный просмотр вперёд: после u_x НЕ должно быть ещё одного x,
        //         что позволяет u_x в конце строки тоже корректно распознать.
        bool ContainsBareUx() =>
            System.Text.RegularExpressions.Regex.IsMatch(eqLower, @"(?:^|[+\-\*=(])u_x(?!x)");

        // То же для u_t: обычная проверка Contains("u_t") истинна и для "u_tt",
        // из-за чего волновое уравнение уходило в ветку теплопроводности.
        bool ContainsBareUt() =>
            System.Text.RegularExpressions.Regex.IsMatch(eqLower, @"(?:^|[+\-\*=(])u_t(?!t)");

        // Уравнение Бюргерса (нелинейное)
        if (eqLower.Contains("u*u_x") || eqLower.Contains("u·u_x"))
            return PDESolver.SolveBurgersEquation(equation);

        // Уравнение Шрёдингера: явный признак ψ_t/psi_t ИЛИ комбинация i и ℏ
        // (просто наличие одиночной 'i' слишком слабо — встречается в sin, pi и т.д.)
        if (eqLower.Contains("ψ_t") || eqLower.Contains("psi_t") ||
            (eqLower.Contains("ℏ") && System.Text.RegularExpressions.Regex.IsMatch(eqLower, @"(?<![a-z])i(?![a-z])")))
            return PDESolver.SolveSchrodingerEquation(equation);

        // Уравнение диффузии 2D (проверяем первым, т.к. содержит и u_t и u_xx и u_yy)
        if (ContainsBareUt() && eqLower.Contains("u_xx") && eqLower.Contains("u_yy"))
            return PDESolver.SolveDiffusionEquation(equation);

        // Уравнение теплопроводности (ВАЖНО: ДО диффузии-адвекции!)
        // Проверяем, что НЕТ явного бесконечно-производного u_x как отдельного слагаемого.
        if (ContainsBareUt() && eqLower.Contains("u_xx") && !eqLower.Contains("u_yy") &&
            !ContainsBareUx())
            return PDESolver.SolveHeatEquation(equation);

        // Уравнение диффузии-адвекции
        if (ContainsBareUt() && ContainsBareUx() &&
            eqLower.Contains("u_xx") && !eqLower.Contains("u_yy"))
            return PDESolver.SolveDiffusionAdvectionEquation(equation);

        // Уравнение адвекции (переноса)
        if (ContainsBareUt() && ContainsBareUx() && !eqLower.Contains("u_xx"))
            return PDESolver.SolveAdvectionEquation(equation);

        // Волновое уравнение
        if (eqLower.Contains("u_tt") && eqLower.Contains("u_xx"))
            return PDESolver.SolveWaveEquation(equation);

        // Уравнение Гельмгольца
        if (eqLower.Contains("u_xx") && eqLower.Contains("u_yy") && eqLower.Contains("=0") &&
            !ContainsBareUt() && !eqLower.Contains("u_tt"))
        {
            var hasUTerm = System.Text.RegularExpressions.Regex.IsMatch(eqLower, @"[+\-]\s*\d*\.?\d*\s*\*?\s*u\s*=\s*0");
            if (hasUTerm)
                return PDESolver.SolveHelmholtzEquation(equation);
        }

        // Уравнение Лапласа (проверяем ПОСЛЕ Гельмгольца, когда точно нет члена с u)
        if (eqLower.Contains("u_xx") && eqLower.Contains("u_yy") && eqLower.Contains("=0") &&
            !ContainsBareUt() && !eqLower.Contains("u_tt"))
            return PDESolver.SolveLaplaceEquation(equation);

        // Уравнение Пуассона
        if (eqLower.Contains("u_xx") && eqLower.Contains("u_yy") && !eqLower.Contains("=0") &&
            !ContainsBareUt() && !eqLower.Contains("u_tt"))
            return PDESolver.SolvePoissonEquation(equation);

        return "Неизвестный тип PDE. Поддерживаются:\n" +
               "  БАЗОВЫЕ (5 из требований):\n" +
               "  * u_t = u_xx                  (теплопроводность)\n" +
               "  * u_tt = c^2*u_xx            (волновое)\n" +
               "  * u_xx + u_yy = 0            (Лапласа)\n" +
               "  * u_xx + u_yy = f(x,y)       (Пуассона)\n" +
               "  * u_t = D*(u_xx + u_yy)      (диффузия 2D)\n\n" +
               "  РАСШИРЕННЫЕ (дополнительно):\n" +
               "  * u_xx + u_yy + k²u = 0      (Гельмгольца)\n" +
               "  * iℏψ_t = -ℏ²/(2m)∇²ψ + Vψ   (Шрёдингера)\n" +
               "  * u_t + c·u_x = 0            (адвекции)\n" +
               "  * u_t + c·u_x = D·u_xx       (диффузии-адвекции)\n" +
               "  * u_t + u·u_x = ν·u_xx       (Бюргерса)";
    }


    protected double EvaluateExpression(Expression expr, Dictionary<string, double> variables)
        => ExpressionEvaluator.Evaluate(expr, variables);

    public static string GetHelpText()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("\n+===========================================================+");
        sb.AppendLine("|                    СПРАВКА ПО КОМАНДАМ                    |");
        sb.AppendLine("+===========================================================+");
        sb.AppendLine();
        sb.AppendLine("ИНТЕГРИРОВАНИЕ:");
        sb.AppendLine("  integrate x^2                         - неопределенный интеграл");
        sb.AppendLine("  integrate sin(x)*cos(x)               - сложная функция");
        sb.AppendLine("  integrate x^2 from 0 to 5             - определенный интеграл");
        sb.AppendLine("  integrate x*exp(x) dx                 - по частям");
        sb.AppendLine("  integrate integrate x*y dx dy         - двойной интеграл");
        sb.AppendLine();
        sb.AppendLine("ДИФФЕРЕНЦИРОВАНИЕ:");
        sb.AppendLine("  derivative of x^3 + 2*x^2             - первая производная");
        sb.AppendLine("  second derivative of sin(x)           - вторая производная");
        sb.AppendLine("  3rd derivative of x^5                 - производная n-го порядка");
        sb.AppendLine("  partial derivative of x^2*y^3 with respect to x");
        sb.AppendLine("  derivative of ln(sin(x^2))            - сложная функция");
        sb.AppendLine();
        sb.AppendLine("ДИФФЕРЕНЦИАЛЬНЫЕ УРАВНЕНИЯ (ODE):");
        sb.AppendLine("  solve y' + 2y = 0                     - линейное ОДУ 1-го порядка");
        sb.AppendLine("  solve y'' + 4y = 0                    - ОДУ 2-го порядка");
        sb.AppendLine("  solve y' = 2x, y(0) = 1               - с начальными условиями");
        sb.AppendLine("  solve x' = y, y' = -x                 - система ОДУ");
        sb.AppendLine("  solve y' = y^2 + x                    - нелинейное ОДУ");
        sb.AppendLine();
        sb.AppendLine("УРАВНЕНИЯ В ЧАСТНЫХ ПРОИЗВОДНЫХ (PDE):");
        sb.AppendLine("  БАЗОВЫЕ (5 из требований):");
        sb.AppendLine("  solve u_t = u_xx                      - уравнение теплопроводности");
        sb.AppendLine("  solve u_tt = c^2 * u_xx               - волновое уравнение");
        sb.AppendLine("  solve u_xx + u_yy = 0                 - уравнение Лапласа");
        sb.AppendLine("  solve u_xx + u_yy = f(x,y)            - уравнение Пуассона");
        sb.AppendLine("  solve u_t = D*(u_xx + u_yy)           - уравнение диффузии 2D");
        sb.AppendLine();
        sb.AppendLine("  РАСШИРЕННЫЕ (дополнительно +5):");
        sb.AppendLine("  solve u_xx + u_yy + 4*u = 0           - уравнение Гельмгольца");
        sb.AppendLine("  solve psi_t = ...                     - уравнение Шрёдингера");
        sb.AppendLine("  solve u_t + 2*u_x = 0                 - уравнение адвекции");
        sb.AppendLine("  solve u_t + u_x = 0.1*u_xx            - диффузия-адвекция");
        sb.AppendLine("  solve u_t + u*u_x = 0.01*u_xx         - уравнение Бюргерса");
        sb.AppendLine();
        sb.AppendLine("ДОПОЛНИТЕЛЬНЫЕ ФУНКЦИИ:");
        sb.AppendLine("  limit (sin(x)/x) as x->0              - предел функции");
        sb.AppendLine("  Taylor series of sin(x) at x=0        - ряд Тейлора");
        sb.AppendLine("  Laplace transform of sin(t)           - преобразование Лапласа");
        sb.AppendLine("  Laplace table                         - таблица преобразований Лапласа");
        sb.AppendLine("  Fourier transform of exp(-x^2)        - преобразование Фурье");
        sb.AppendLine("  solve x^2 + 5*x + 6 = 0               - решение уравнений");
        sb.AppendLine();

        return sb.ToString();
    }
}
