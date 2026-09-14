#nullable enable
using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Приближение окружности и эллипса вписанным многоугольником.
/// </summary>
public static class Polygonization
{
    /// <summary>
    /// Правильный многоугольник, вписанный в окружность; первая вершина лежит на луче от центра вдоль оси x.
    /// </summary>
    /// <param name="circle">Окружность.</param>
    /// <param name="count">Число вершин, не меньше трех.</param>
    /// <returns>Вершины против часовой стрелки.</returns>
    public static Vector[] FromCircle(Circle circle, int count)
    {
        ArgumentNullException.ThrowIfNull(circle);
        return FromEllipse(new Ellipse(circle.Center, circle.Radius, circle.Radius, 0), count);
    }

    /// <summary>
    /// Многоугольник, вписанный в эллипс: вершины при равномерном шаге параметра <see cref="Ellipse.PointAt"/>.
    /// </summary>
    /// <param name="ellipse">Эллипс.</param>
    /// <param name="count">Число вершин, не меньше трех.</param>
    /// <returns>Вершины против часовой стрелки (при положительных полуосях).</returns>
    public static Vector[] FromEllipse(Ellipse ellipse, int count)
    {
        ArgumentNullException.ThrowIfNull(ellipse);
        if (count < 3)
            throw new ArgumentOutOfRangeException(nameof(count), "У многоугольника не меньше трех вершин");

        var vertices = new Vector[count];
        for (int k = 0; k < count; k++)
            vertices[k] = ellipse.PointAt(2 * Math.PI * k / count);
        return vertices;
    }
}
