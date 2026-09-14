#nullable enable
using System;
using AI.Geometry.Polygons;
using AI.Geometry.Primitives;
using AI.Geometry.Triangulation;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Sampling;

/// <summary>
/// Равномерные случайные точки в простом многоугольнике на плоскости, в том числе с дырами.
/// </summary>
/// <remarks>
/// Многоугольник разбивается на треугольники отсечением ушей (<see cref="EarClipping"/>); треугольник выбирается
/// с вероятностью, пропорциональной его площади, а точка в нем берется равномерно. Первое равномерное число
/// выбирает треугольник и, растянутое на него, вместе со вторым задает точку внутри, поэтому выборка принимает
/// и готовые числа, например точки последовательностей Холтона и Соболя. Самопересекающийся контур
/// не поддерживается.
/// </remarks>
public sealed class PolygonSampler
{
    private readonly Triangle[] _triangles;
    private readonly double[] _cumulativeAreas;

    /// <summary>Готовит выборку в многоугольнике</summary>
    /// <param name="polygon">Вершины простого многоугольника по порядку обхода, двумерные</param>
    /// <param name="holes">Дыры: простые многоугольники внутри контура, не пересекающие друг друга</param>
    public PolygonSampler(Vector[] polygon, params Vector[][] holes)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        ArgumentNullException.ThrowIfNull(holes);
        CheckContour(polygon, nameof(polygon));

        double expected = ShoelaceArea.Area(polygon);
        double scale = expected;

        foreach (var hole in holes)
        {
            CheckContour(hole, nameof(holes));
            double holeArea = ShoelaceArea.Area(hole);
            expected -= holeArea;
            scale += holeArea;
        }

        var triangles = EarClipping.Triangulate(polygon, holes);
        _triangles = new Triangle[triangles.Count];
        _cumulativeAreas = new double[triangles.Count];
        double total = 0;

        for (int i = 0; i < _triangles.Length; i++)
        {
            _triangles[i] = triangles[i];
            total += triangles[i].Area();
            _cumulativeAreas[i] = total;
        }

        if (!(total > 0))
            throw new ArgumentException("Площадь многоугольника должна быть положительной", nameof(polygon));

        // Разбиение самопересекающегося контура или выходящих за него дыр не сходится по площади с формулой шнурка
        if (Math.Abs(total - expected) > 1e-7 * scale)
            throw new ArgumentException("Контур самопересекается или дыры выходят за него", nameof(polygon));

        Area = total;
    }

    /// <summary>Площадь многоугольника за вычетом дыр</summary>
    public double Area { get; }

    /// <summary>Равномерная точка в многоугольнике</summary>
    /// <param name="rng">Генератор</param>
    public Vector Sample(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        return Sample(rng.NextDouble(), rng.NextDouble());
    }

    /// <summary>Точка в многоугольнике по двум равномерным числам</summary>
    /// <param name="u1">Первое равномерное число из [0; 1], выбирает треугольник разбиения</param>
    /// <param name="u2">Второе равномерное число из [0; 1]</param>
    public Vector Sample(double u1, double u2)
    {
        PlaneSampling.CheckUnit(u1, nameof(u1));
        PlaneSampling.CheckUnit(u2, nameof(u2));

        double target = u1 * _cumulativeAreas[^1];
        int index = Array.BinarySearch(_cumulativeAreas, target);
        index = index >= 0 ? index : ~index;
        index = Math.Min(index, _triangles.Length - 1);

        double below = index == 0 ? 0 : _cumulativeAreas[index - 1];
        double width = _cumulativeAreas[index] - below;
        double local = width > 0 ? Math.Clamp((target - below) / width, 0, 1) : 0;

        return PlaneSampling.InTriangle(local, u2, _triangles[index]);
    }

    private static void CheckContour(Vector[] contour, string name)
    {
        if (contour is null || contour.Length < 3)
            throw new ArgumentException("У многоугольника должно быть не меньше трех вершин", name);

        foreach (var vertex in contour)
        {
            if (vertex is null || vertex.Count != 2)
                throw new ArgumentException("Вершины многоугольника должны быть двумерными", name);
        }
    }
}
