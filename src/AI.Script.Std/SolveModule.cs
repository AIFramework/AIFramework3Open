using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Solvers.Math.Core.Numerics;
using AI.Solvers.Math.Core.Solvers;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>solve</c>: символьная математика и численные методы.
/// </summary>
/// <remarks>
/// Символьная часть работает со строками — так устроен <c>AI.Solvers.Math</c>, и заводить в
/// языке отдельный тип «выражение» ради неё преждевременно: выражение всё равно приходит из
/// текста и уходит в текст.
/// <para>
/// Численная часть принимает функцию языка. Вызов синхронный (см. <see cref="ScriptCallbacks"/>):
/// тело лямбды на языке синхронно, и квадратура не должна платить за асинхронность, которой
/// в ней нет.
/// </para>
/// </remarks>
[ScriptModule("solve", "Символьная математика и численные методы: производные, интегралы, корни, ОДУ", Version = "0.1")]
public static class SolveModule
{
    [ScriptFn("diff", "Символьная производная выражения", Example = "solve.diff(\"x^2 + 3*x\", by: \"x\")")]
    public static string Differentiate(
        [ScriptParam("выражение")] string expression,
        [ScriptParam("переменная")] string by = "x",
        [ScriptParam("порядок производной")] int order = 1)
    {
        RequireExpression(expression, "solve.diff");

        if (order < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "solve.diff: порядок должен быть не меньше 1");

        return Guarded("solve.diff", () => order == 1
            ? DerivativeSolver.FirstDerivative(expression, by)
            : DerivativeSolver.NthDerivative(expression, by, order));
    }

    [ScriptFn("integrate", "Символьный неопределённый интеграл", Example = "solve.integrate(\"2*x\", by: \"x\")")]
    public static string Integrate(
        [ScriptParam("выражение")] string expression,
        [ScriptParam("переменная")] string by = "x")
    {
        RequireExpression(expression, "solve.integrate");

        return Guarded("solve.integrate", () => IntegralSolver.IndefiniteIntegral(expression, by));
    }

    [ScriptFn("integrate_range", "Символьный определённый интеграл",
        Example = "solve.integrate_range(\"x^2\", by: \"x\", from: 0, to: 1)")]
    public static string IntegrateRange(
        [ScriptParam("выражение")] string expression,
        [ScriptParam("переменная")] string by,
        [ScriptParam("нижний предел")] double from,
        [ScriptParam("верхний предел")] double to)
    {
        RequireExpression(expression, "solve.integrate_range");

        return Guarded("solve.integrate_range", () => IntegralSolver.DefiniteIntegral(expression, by, from, to));
    }

    [ScriptFn("equation", "Решает уравнение символьно", Example = "solve.equation(\"x^2 - 4 = 0\")")]
    public static string Equation([ScriptParam("уравнение со знаком =")] string equation)
    {
        RequireExpression(equation, "solve.equation");

        return Guarded("solve.equation", () => AdvancedSolver.SolveEquation(equation));
    }

    [ScriptFn("ode", "Решает обыкновенное дифференциальное уравнение символьно",
        Example = "solve.ode(\"y' + 2*y = 0\")")]
    public static string Ode(
        [ScriptParam("уравнение")] string equation,
        [ScriptParam("порядок: 1 либо 2")] int order = 1)
    {
        RequireExpression(equation, "solve.ode");

        return Guarded("solve.ode", () => order switch
        {
            1 => ODESolver.SolveFirstOrderLinearODE(equation),
            2 => ODESolver.SolveSecondOrderODE(equation),
            _ => throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"solve.ode: порядок {order} не поддержан",
                "символьно решаются уравнения первого и второго порядка"),
        });
    }

    // --- численные методы ---

    /// <summary>
    /// Численное интегрирование функции языка.
    /// </summary>
    /// <remarks>
    /// Адаптивная квадратура фреймворка: число вызовов подынтегральной функции заранее
    /// неизвестно, поэтому тяжёлая лямбда здесь обойдётся дорого — это стоит знать до, а не
    /// после запуска.
    /// </remarks>
    [ScriptFn("integrate_fn", "Численный определённый интеграл функции",
        Example = "solve.integrate_fn(x => x * x, from: 0, to: 1)")]
    public static double IntegrateFunction(
        IScriptContext context,
        [ScriptParam("подынтегральная функция")] ScriptCallable f,
        [ScriptParam("нижний предел")] double from,
        [ScriptParam("верхний предел")] double to,
        [ScriptParam("точность")] double tolerance = 1e-10)
    {
        Func<double, double> integrand = ScriptCallbacks.AsFunction(context, f, "solve.integrate_fn: значение функции");

        return Guarded("solve.integrate_fn", () => Quadrature.Integrate(integrand, from, to, tolerance));
    }

    [ScriptFn("root", "Численный корень уравнения f(x) = 0 на отрезке",
        Example = "solve.root(x => x * x - 2, from: 0, to: 2)")]
    public static double Root(
        IScriptContext context,
        [ScriptParam("функция")] ScriptCallable f,
        [ScriptParam("левая граница отрезка")] double from,
        [ScriptParam("правая граница отрезка")] double to,
        [ScriptParam("точность")] double tolerance = 1e-10)
    {
        Func<double, double> function = ScriptCallbacks.AsFunction(context, f, "solve.root: значение функции");

        (bool success, double root, _) = NumericalEquationSolver.Bisection(function, from, to, tolerance);

        if (!success)
        {
            throw new ScriptError(
                DiagnosticCodes.FunctionFailed,
                $"solve.root: корень не найден на отрезке [{ScriptFormatter.Number(from)}, {ScriptFormatter.Number(to)}]",
                "метод деления пополам требует, чтобы функция меняла знак на концах отрезка");
        }

        return root;
    }

    [ScriptFn("roots", "Все корни функции на отрезке", Example = "solve.roots(x => math.sin(x), from: 0, to: 10)")]
    public static Vector Roots(
        IScriptContext context,
        [ScriptParam("функция")] ScriptCallable f,
        [ScriptParam("левая граница отрезка")] double from,
        [ScriptParam("правая граница отрезка")] double to,
        [ScriptParam("число проб для локализации корней")] int samples = 100)
    {
        Func<double, double> function = ScriptCallbacks.AsFunction(context, f, "solve.roots: значение функции");

        List<double> found = Guarded("solve.roots",
            () => NumericalEquationSolver.FindAllRoots(function, from, to, samples));

        return new Vector(found);
    }

    [ScriptFn("derivative_fn", "Численная производная функции в точке",
        Example = "solve.derivative_fn(x => x * x, at: 3)")]
    public static double DerivativeAt(
        IScriptContext context,
        [ScriptParam("функция")] ScriptCallable f,
        [ScriptParam("точка")] double at,
        [ScriptParam("шаг; 0 — выбрать автоматически")] double step = 0)
    {
        Func<double, double> function = ScriptCallbacks.AsFunction(context, f, "solve.derivative_fn: значение функции");

        return NumericalEquationSolver.NumericalDerivative(function, step)(at);
    }

    private static void RequireExpression(string expression, string what)
    {
        if (!string.IsNullOrWhiteSpace(expression)) return;

        throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: пустое выражение");
    }

    /// <summary>
    /// Оборачивает отказ решателя в понятную диагностику.
    /// </summary>
    /// <remarks>
    /// Символьные решатели отказывают часто и законно — не всякий интеграл берётся. Отказ
    /// должен называть функцию и причину, а не всплывать голым исключением библиотеки.
    /// </remarks>
    private static T Guarded<T>(string what, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (ScriptError)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ScriptError(
                DiagnosticCodes.FunctionFailed,
                $"{what}: {exception.GetType().Name} — {exception.Message}",
                exception);
        }
    }
}
