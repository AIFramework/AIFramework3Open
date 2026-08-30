using AI.DataStructs.Algebraic;

namespace AI.Solvers.Pde.FiniteDifference;

/// <summary>Решение одномерного волнового уравнения</summary>
public sealed class WaveSolution
{
    internal WaveSolution(Grid1D grid, Vector values, Vector previous, int steps, double courant)
    {
        Grid = grid;
        Values = values;
        Previous = previous;
        Steps = steps;
        Courant = courant;
    }

    /// <summary>Сетка по пространству</summary>
    public Grid1D Grid { get; }

    /// <summary>Значения на конечный момент времени</summary>
    public Vector Values { get; }

    /// <summary>Значения на предпоследнем слое — нужны для оценки скорости</summary>
    public Vector Previous { get; }

    /// <summary>Число шагов по времени</summary>
    public int Steps { get; }

    /// <summary>Число Куранта <c>c·Δt/h</c></summary>
    public double Courant { get; }

    /// <summary>Устойчива ли схема: число Куранта не больше единицы</summary>
    public bool IsStable => Courant <= 1.0 + 1e-12;

    /// <summary>Краткая запись результата</summary>
    public override string ToString() => $"волновое уравнение: узлов {Grid.Count}, шагов {Steps}, CFL = {Courant:F4}";
}

/// <summary>
/// Волновое уравнение <c>u_tt = c²·u_xx</c> на отрезке с условиями Дирихле.
/// </summary>
/// <remarks>
/// <para>
/// Явная трёхслойная схема второго порядка. Условие Куранта <c>c·Δt/h ≤ 1</c> здесь не
/// рекомендация: при его нарушении решение растёт экспоненциально, поэтому решатель
/// отказывается считать, а не выдаёт красивый мусор.
/// </para>
/// <para>
/// Первый слой считается отдельно по разложению Тейлора с учётом начальной скорости:
/// <c>u¹ = u⁰ + Δt·ψ + ½r²·δ²u⁰</c>. Если стартовать общей трёхслойной формулой,
/// схема теряет второй порядок.
/// </para>
/// </remarks>
public static class WaveEquation1D
{
    /// <summary>
    /// Решает волновое уравнение
    /// </summary>
    /// <param name="grid">Сетка по пространству</param>
    /// <param name="speed">Скорость распространения c</param>
    /// <param name="initialDisplacement">Начальное отклонение</param>
    /// <param name="initialVelocity">Начальная скорость; по умолчанию нулевая</param>
    /// <param name="finalTime">Конечное время</param>
    /// <param name="steps">Число шагов по времени</param>
    /// <exception cref="ArgumentException">Нарушено условие Куранта</exception>
    public static WaveSolution Solve(
        Grid1D grid,
        double speed,
        Func<double, double> initialDisplacement,
        Func<double, double>? initialVelocity,
        double finalTime,
        int steps)
    {
        grid.Validate();
        ArgumentNullException.ThrowIfNull(initialDisplacement);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(steps);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalTime);

        int count = grid.Count;
        double step = grid.Step;
        double dt = finalTime / steps;
        double courant = speed * dt / step;

        if (courant > 1.0 + 1e-12)
            throw new ArgumentException(
                $"Нарушено условие Куранта: c·Δt/h = {courant:F4} > 1. Уменьшите шаг по времени или огрубите сетку.",
                nameof(steps));

        Vector previous = grid.Sample(initialDisplacement);
        previous[0] = 0;
        previous[count - 1] = 0;

        var current = new Vector(count);
        double square = courant * courant;

        for (int i = 1; i < count - 1; i++)
        {
            double velocity = initialVelocity?.Invoke(grid.Node(i)) ?? 0.0;
            double curvature = previous[i + 1] - (2 * previous[i]) + previous[i - 1];

            current[i] = previous[i] + (dt * velocity) + (0.5 * square * curvature);
        }

        for (int n = 1; n < steps; n++)
        {
            var next = new Vector(count);

            for (int i = 1; i < count - 1; i++)
                next[i] = (2 * current[i]) - previous[i]
                    + (square * (current[i + 1] - (2 * current[i]) + current[i - 1]));

            previous = current;
            current = next;
        }

        return new WaveSolution(grid, current, previous, steps, courant);
    }
}
