#nullable enable
using System;
using AI.Statistics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Transforms;

/// <summary>
/// Полярные координаты и азимуты на плоскости. Углы в радианах, в двух договорённостях:
/// математической — от оси X против часовой стрелки, как в <see cref="Affine2D"/>, — и навигационной —
/// азимут от севера по часовой стрелке, как у компаса и в <c>AI.Earth.Geodesy</c>. В навигационной
/// север — ось +Y, восток — ось +X.
/// </summary>
public static class Polar
{
    /// <summary>
    /// Точка по радиусу и углу от оси X против часовой стрелки.
    /// </summary>
    public static Vector ToCartesian(double radius, double angle)
    {
        return new Vector(radius * Math.Cos(angle), radius * Math.Sin(angle));
    }

    /// <summary>
    /// Радиус и угол от оси X против часовой стрелки; угол на [0; 2π).
    /// </summary>
    public static (double Radius, double Angle) FromCartesian(Vector point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return (Math.Sqrt((point[0] * point[0]) + (point[1] * point[1])), CircularStatistics.WrapRadians(Math.Atan2(point[1], point[0])));
    }

    /// <summary>
    /// Точка на заданном расстоянии по азимуту от начала координат: север — +Y, восток — +X.
    /// </summary>
    public static Vector FromBearing(double distance, double bearing)
    {
        return new Vector(distance * Math.Sin(bearing), distance * Math.Cos(bearing));
    }

    /// <summary>
    /// Азимут от одной точки на другую на [0; 2π): 0 — север, π/2 — восток.
    /// </summary>
    public static double Bearing(Vector from, Vector to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        return CircularStatistics.WrapRadians(Math.Atan2(to[0] - from[0], to[1] - from[1]));
    }
}
