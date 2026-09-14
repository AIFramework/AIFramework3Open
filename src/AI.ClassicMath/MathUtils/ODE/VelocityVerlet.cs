#nullable enable

using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.MathUtils.ODE;

/// <summary>
/// Скоростной метод Верле для систем второго порядка x'' = a(x, v, t).
/// </summary>
/// <remarks>
/// Если ускорение не зависит от скорости, метод симплектический и обратимый во времени, имеет второй порядок,
/// а ошибка энергии консервативной системы остается ограниченной на сколь угодно долгом интервале (без дрейфа).
/// При зависимости от скорости (трение, магнитная сила) скорость в конце шага оценивается явным прогнозом v + h·a;
/// метод остается явным и второго порядка по координате, но симплектичность теряется.
/// </remarks>
public static class VelocityVerlet
{
    /// <summary>
    /// Интегрирует систему с постоянным шагом.
    /// </summary>
    /// <param name="acceleration">Ускорение a(x, v, t).</param>
    /// <param name="position">Начальные координаты.</param>
    /// <param name="velocity">Начальные скорости.</param>
    /// <param name="startTime">Начальный момент.</param>
    /// <param name="step">Шаг по времени (может быть отрицательным для обратного хода).</param>
    /// <param name="steps">Число шагов.</param>
    /// <param name="recordEvery">Записывать каждую recordEvery-ю точку; начальная и последняя записываются всегда.</param>
    /// <returns>Записанные моменты, координаты и скорости.</returns>
    public static VerletTrajectory Integrate(
        Func<Vector, Vector, double, Vector> acceleration,
        Vector position,
        Vector velocity,
        double startTime,
        double step,
        int steps,
        int recordEvery = 1)
    {
        ArgumentNullException.ThrowIfNull(acceleration);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(velocity);

        if (position.Count != velocity.Count)
            throw new ArgumentException("Размеры векторов координат и скоростей различаются.", nameof(velocity));

        if (steps < 0)
            throw new ArgumentOutOfRangeException(nameof(steps), "Число шагов не может быть отрицательным.");

        if (recordEvery < 1)
            throw new ArgumentOutOfRangeException(nameof(recordEvery), "Шаг записи должен быть положительным.");

        int n = position.Count;
        double[] x = position.ToArray();
        double[] v = velocity.ToArray();
        double[] a = Accelerate(acceleration, x, v, startTime, n);
        var halfVelocity = new double[n];
        var predicted = new double[n];

        var times = new List<double> { startTime };
        var positions = new List<Vector> { new Vector(x) };
        var velocities = new List<Vector> { new Vector(v) };

        for (int s = 1; s <= steps; s++)
        {
            double time = startTime + (s * step);

            for (int i = 0; i < n; i++)
            {
                halfVelocity[i] = v[i] + (0.5 * step * a[i]);
                x[i] += step * halfVelocity[i];
                predicted[i] = v[i] + (step * a[i]);
            }

            a = Accelerate(acceleration, x, predicted, time, n);

            for (int i = 0; i < n; i++)
                v[i] = halfVelocity[i] + (0.5 * step * a[i]);

            if (s % recordEvery == 0 || s == steps)
            {
                times.Add(time);
                positions.Add(new Vector(x));
                velocities.Add(new Vector(v));
            }
        }

        return new VerletTrajectory(new Vector(times), positions, velocities);
    }

    private static double[] Accelerate(Func<Vector, Vector, double, Vector> acceleration, double[] x, double[] v, double t, int n)
    {
        Vector value = acceleration(new Vector(x), new Vector(v), t)
            ?? throw new InvalidOperationException("Функция ускорения вернула null.");

        if (value.Count != n)
            throw new InvalidOperationException($"Функция ускорения вернула {value.Count} значений вместо {n}.");

        return value.ToArray();
    }
}
