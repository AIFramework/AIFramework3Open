using AI.DataStructs.Algebraic;
using AI.Solvers.Pde.Numerics;

namespace AI.Solvers.Pde.FiniteDifference;

/// <summary>Схема интегрирования по времени</summary>
public enum TimeScheme
{
    /// <summary>
    /// Явная: значение на новом слое считается напрямую. Требует шага
    /// <c>Δt ≤ h²/(2α)</c>, иначе решение разваливается.
    /// </summary>
    Explicit,

    /// <summary>
    /// Неявная схема Кранка — Николсон: устойчива при любом шаге,
    /// второй порядок и по времени, и по пространству. Требует решения
    /// трёхдиагональной системы на каждом шаге.
    /// </summary>
    CrankNicolson
}

/// <summary>Решение одномерного уравнения теплопроводности</summary>
public sealed class HeatSolution
{
    internal HeatSolution(Grid1D grid, Vector values, int steps, double courant, bool stable)
    {
        Grid = grid;
        Values = values;
        Steps = steps;
        Courant = courant;
        IsStable = stable;
    }

    /// <summary>Сетка по пространству</summary>
    public Grid1D Grid { get; }

    /// <summary>Значения в узлах на конечный момент времени</summary>
    public Vector Values { get; }

    /// <summary>Число шагов по времени</summary>
    public int Steps { get; }

    /// <summary>Сеточное число <c>α·Δt/h²</c></summary>
    public double Courant { get; }

    /// <summary>Устойчива ли использованная схема при этих параметрах</summary>
    public bool IsStable { get; }

    /// <summary>Краткая запись результата</summary>
    public override string ToString()
        => $"теплопроводность: узлов {Grid.Count}, шагов {Steps}, α·Δt/h² = {Courant:F4}";
}

/// <summary>
/// Уравнение теплопроводности <c>u_t = α·u_xx + f(x, t)</c> на отрезке
/// с условиями Дирихле на концах.
/// </summary>
/// <remarks>
/// В отличие от демонстрационных решателей <c>AI.Solvers.Math.Core.Solvers.NumericalPDESolver</c>,
/// печатающих отчёт для фиксированной задачи, здесь задаются собственная область, начальное
/// условие, источник и граничные значения, а результатом служит поле, пригодное для дальнейших
/// вычислений.
/// </remarks>
public static class HeatEquation1D
{
    /// <summary>
    /// Решает уравнение теплопроводности
    /// </summary>
    /// <param name="grid">Сетка по пространству</param>
    /// <param name="diffusivity">Коэффициент температуропроводности α</param>
    /// <param name="initial">Начальное распределение</param>
    /// <param name="leftBoundary">Значение на левой границе в зависимости от времени</param>
    /// <param name="rightBoundary">Значение на правой границе в зависимости от времени</param>
    /// <param name="finalTime">Конечное время</param>
    /// <param name="steps">Число шагов по времени</param>
    /// <param name="scheme">Схема интегрирования</param>
    /// <param name="source">Источник <c>f(x, t)</c>; по умолчанию отсутствует</param>
    public static HeatSolution Solve(
        Grid1D grid,
        double diffusivity,
        Func<double, double> initial,
        Func<double, double> leftBoundary,
        Func<double, double> rightBoundary,
        double finalTime,
        int steps,
        TimeScheme scheme = TimeScheme.CrankNicolson,
        Func<double, double, double>? source = null)
    {
        grid.Validate();
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(leftBoundary);
        ArgumentNullException.ThrowIfNull(rightBoundary);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(steps);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalTime);

        int count = grid.Count;
        double step = grid.Step;
        double dt = finalTime / steps;
        double r = diffusivity * dt / (step * step);

        bool stable = scheme == TimeScheme.CrankNicolson || r <= 0.5;

        Vector current = grid.Sample(initial);
        current[0] = leftBoundary(0);
        current[count - 1] = rightBoundary(0);

        for (int n = 0; n < steps; n++)
        {
            double time = n * dt;
            double nextTime = (n + 1) * dt;

            current = scheme == TimeScheme.Explicit
                ? ExplicitStep(grid, current, r, dt, time, source)
                : CrankNicolsonStep(grid, current, r, dt, time, source);

            current[0] = leftBoundary(nextTime);
            current[count - 1] = rightBoundary(nextTime);
        }

        return new HeatSolution(grid, current, steps, r, stable);
    }

    private static Vector ExplicitStep(
        Grid1D grid, Vector current, double r, double dt, double time, Func<double, double, double>? source)
    {
        int count = grid.Count;
        var next = new Vector(count);

        for (int i = 1; i < count - 1; i++)
        {
            double diffusion = r * (current[i + 1] - (2 * current[i]) + current[i - 1]);
            double forcing = source is null ? 0.0 : dt * source(grid.Node(i), time);

            next[i] = current[i] + diffusion + forcing;
        }

        return next;
    }

    /// <summary>
    /// Шаг схемы Кранка — Николсон: полусумма явной и неявной аппроксимаций.
    /// </summary>
    private static Vector CrankNicolsonStep(
        Grid1D grid, Vector current, double r, double dt, double time, Func<double, double, double>? source)
    {
        int count = grid.Count;

        var lower = new Vector(count);
        var diagonal = new Vector(count);
        var upper = new Vector(count);
        var right = new Vector(count);

        // Границы держатся условием Дирихле: строка тождественная
        diagonal[0] = 1.0;
        right[0] = current[0];
        diagonal[count - 1] = 1.0;
        right[count - 1] = current[count - 1];

        double half = r / 2.0;

        for (int i = 1; i < count - 1; i++)
        {
            lower[i] = -half;
            diagonal[i] = 1.0 + r;
            upper[i] = -half;

            double explicitPart = current[i] + (half * (current[i + 1] - (2 * current[i]) + current[i - 1]));
            double forcing = source is null
                ? 0.0
                : dt * 0.5 * (source(grid.Node(i), time) + source(grid.Node(i), time + dt));

            right[i] = explicitPart + forcing;
        }

        return IterativeSolvers.SolveTridiagonal(lower, diagonal, upper, right);
    }
}
