#nullable enable

using AI.DataStructs.Algebraic;
using System.Collections.Generic;

namespace AI.MathUtils.ODE;

/// <summary>
/// Траектория системы второго порядка, полученная методом Верле.
/// </summary>
public sealed class VerletTrajectory
{
    internal VerletTrajectory(Vector times, IReadOnlyList<Vector> positions, IReadOnlyList<Vector> velocities)
    {
        Times = times;
        Positions = positions;
        Velocities = velocities;
    }

    /// <summary>
    /// Моменты записанных точек.
    /// </summary>
    public Vector Times { get; }

    /// <summary>
    /// Координаты в моменты <see cref="Times"/>.
    /// </summary>
    public IReadOnlyList<Vector> Positions { get; }

    /// <summary>
    /// Скорости в моменты <see cref="Times"/>.
    /// </summary>
    public IReadOnlyList<Vector> Velocities { get; }
}
