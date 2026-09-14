#nullable enable

using System;
using System.Collections.Generic;
using AI.DataStructs.Algebraic;

namespace AI.Geometry.MassProperties;

/// <summary>
/// Объем симплекса в пространстве любой размерности: отрезка, треугольника, тетраэдра и их многомерных аналогов.
/// </summary>
/// <remarks>
/// Симплекс на k + 1 вершинах имеет k-мерный объем √det(G) / k!, где G это матрица Грама ребер из первой вершины.
/// Формула не требует, чтобы размерность пространства n совпадала с k: треугольник в трехмерном пространстве дает
/// площадь, а при k &gt; n симплекс вырожден и объем нулевой.
/// </remarks>
public static class SimplexVolume
{
    /// <summary>
    /// k-мерный объем симплекса на k + 1 вершинах.
    /// </summary>
    /// <param name="vertices">Вершины одной размерности, хотя бы две</param>
    /// <returns>Длина отрезка, площадь треугольника, объем тетраэдра и так далее; 0 у вырожденного симплекса</returns>
    /// <exception cref="ArgumentNullException">Вершины не заданы</exception>
    /// <exception cref="ArgumentException">Вершин меньше двух или у них разная размерность</exception>
    public static double Volume(IReadOnlyList<Vector> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if (vertices.Count < 2)
            throw new ArgumentException("У симплекса должно быть хотя бы две вершины", nameof(vertices));

        int k = vertices.Count - 1, n = vertices[0].Count;
        var edges = new double[k][];
        for (int i = 0; i < k; i++)
        {
            if (vertices[i + 1].Count != n)
                throw new ArgumentException("У вершин симплекса разная размерность", nameof(vertices));
            edges[i] = new double[n];
            for (int d = 0; d < n; d++)
                edges[i][d] = vertices[i + 1][d] - vertices[0][d];
        }

        var gram = new Matrix(k, k);
        for (int i = 0; i < k; i++)
        {
            for (int j = i; j < k; j++)
            {
                double dot = 0;
                for (int d = 0; d < n; d++)
                    dot += edges[i][d] * edges[j][d];
                gram[i, j] = dot;
                gram[j, i] = dot;
            }
        }

        double factorial = 1;
        for (int i = 2; i <= k; i++)
            factorial *= i;

        // Определитель Грама неотрицателен; отрицательный остаток у вырожденного симплекса это ошибка округления
        return Math.Sqrt(Math.Max(0, gram.Determinant)) / factorial;
    }
}
