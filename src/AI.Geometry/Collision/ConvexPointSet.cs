#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Выпуклая оболочка набора точек. Оболочка не строится: опорная точка это точка с наибольшей проекцией на направление
/// </summary>
public sealed class ConvexPointSet : IConvexShape
{
    private readonly Vector3[] _points;
    private readonly Vector3 _localCenter;

    /// <summary>Создает оболочку точек, заданных в локальных координатах</summary>
    /// <param name="pose">Положение локальных осей</param>
    /// <param name="points">Точки в локальных координатах, хотя бы одна</param>
    public ConvexPointSet(Pose pose, IEnumerable<Vector3> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = [.. points];
        if (_points.Length == 0)
            throw new ArgumentException("Нужна хотя бы одна точка", nameof(points));

        Pose = pose;
        var sum = Vector3.Zero;
        foreach (var point in _points)
            sum += point;
        _localCenter = sum / _points.Length;
    }

    /// <summary>Положение локальных осей</summary>
    public Pose Pose { get; }

    /// <summary>Точки в локальных координатах</summary>
    public IReadOnlyList<Vector3> Points => _points;

    /// <inheritdoc/>
    public Vector3 Center => Pose.ToWorld(_localCenter);

    /// <inheritdoc/>
    public Vector3 Support(Vector3 direction)
    {
        var local = Pose.InverseRotate(direction);
        var best = _points[0];
        double bestDot = best.Dot(local);
        for (int i = 1; i < _points.Length; i++)
        {
            double dot = _points[i].Dot(local);
            if (dot > bestDot)
                (best, bestDot) = (_points[i], dot);
        }

        return Pose.ToWorld(best);
    }
}
