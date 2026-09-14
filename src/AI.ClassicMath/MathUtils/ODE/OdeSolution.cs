#nullable enable

using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.MathUtils.ODE;

/// <summary>
/// Решение задачи Коши адаптивным интегратором: узлы траектории, события и плотная выдача между узлами.
/// </summary>
public sealed class OdeSolution
{
    private readonly IReadOnlyList<double> _steps;
    private readonly IReadOnlyList<double[]> _dense;

    internal OdeSolution(
        Vector times,
        IReadOnlyList<Vector> states,
        IReadOnlyList<OdeEventHit> events,
        IReadOnlyList<double> steps,
        IReadOnlyList<double[]> dense)
    {
        Times = times;
        States = states;
        Events = events;
        _steps = steps;
        _dense = dense;
    }

    /// <summary>
    /// Моменты времени принятых шагов, включая начальный и конечный.
    /// </summary>
    public Vector Times { get; }

    /// <summary>
    /// Состояния в моменты <see cref="Times"/>.
    /// </summary>
    public IReadOnlyList<Vector> States { get; }

    /// <summary>
    /// Наступившие события в порядке времени.
    /// </summary>
    public IReadOnlyList<OdeEventHit> Events { get; }

    /// <summary>
    /// Дошло ли интегрирование до конечного момента или до останавливающего события.
    /// false означает исчерпание лимита шагов или слишком малый шаг.
    /// </summary>
    public bool Success { get; internal init; }

    /// <summary>
    /// Остановлено ли интегрирование событием.
    /// </summary>
    public bool StoppedByEvent { get; internal init; }

    /// <summary>
    /// Число принятых шагов.
    /// </summary>
    public int AcceptedSteps { get; internal init; }

    /// <summary>
    /// Число отвергнутых шагов.
    /// </summary>
    public int RejectedSteps { get; internal init; }

    /// <summary>
    /// Число вычислений правой части.
    /// </summary>
    public int FunctionEvaluations { get; internal init; }

    /// <summary>
    /// Последний момент траектории.
    /// </summary>
    public double FinalTime => Times[Times.Count - 1];

    /// <summary>
    /// Последнее состояние траектории.
    /// </summary>
    public Vector FinalState => States[States.Count - 1];

    /// <summary>
    /// Состояние в произвольный момент внутри пройденного отрезка по плотной выдаче 4-го порядка.
    /// </summary>
    /// <param name="time">Момент между первым и последним узлом траектории.</param>
    public Vector Interpolate(double time)
    {
        int count = Times.Count;
        double direction = Times[count - 1] >= Times[0] ? 1 : -1;
        double first = direction * Times[0];
        double last = direction * Times[count - 1];
        double target = direction * time;

        if (target < first - 1e-12 * Math.Max(1, Math.Abs(first)) || target > last + 1e-12 * Math.Max(1, Math.Abs(last)))
            throw new ArgumentOutOfRangeException(nameof(time), "Момент вне пройденного отрезка.");

        if (count == 1)
            return States[0].Clone();

        // Двоичный поиск отрезка [Times[i], Times[i + 1]], содержащего момент
        int low = 0;
        int high = count - 2;

        while (low < high)
        {
            int middle = (low + high + 1) / 2;

            if (direction * Times[middle] <= target)
                low = middle;
            else
                high = middle - 1;
        }

        return new Vector(DenseState(_dense[low], Times[low], _steps[low], time));
    }

    /// <summary>
    /// Значение плотной выдачи Дормана-Принса внутри шага.
    /// Коэффициенты хранятся подряд: r1, r2, r3, r4, r5, каждый длиной n.
    /// </summary>
    internal static double[] DenseState(double[] coefficients, double start, double step, double time)
    {
        int n = coefficients.Length / 5;
        double theta = (time - start) / step;
        double rest = 1 - theta;
        var state = new double[n];

        for (int i = 0; i < n; i++)
        {
            double r1 = coefficients[i];
            double r2 = coefficients[n + i];
            double r3 = coefficients[(2 * n) + i];
            double r4 = coefficients[(3 * n) + i];
            double r5 = coefficients[(4 * n) + i];
            state[i] = r1 + (theta * (r2 + (rest * (r3 + (theta * (r4 + (rest * r5)))))));
        }

        return state;
    }
}
