using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Тест пересечения двух OBB методом разделяющих осей (SAT) — 15 осей для 3D.
/// </summary>
public static class ObbObbIntersection
{
    /// <summary>
    /// Проверяет, пересекаются ли два OBB.
    /// </summary>
    public static bool Test(Obb a, Obb b)
    {
        var axesA = GetAxes(a);
        var axesB = GetAxes(b);
        var t = b.Center - a.Center;

        double[,] R = new double[3, 3];
        double[,] absR = new double[3, 3];

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                R[i, j] = Vector.Dot(axesA[i], axesB[j]);
                absR[i, j] = Math.Abs(R[i, j]) + 1e-15;
            }

        // Тест 3 осей A
        for (int i = 0; i < 3; i++)
        {
            double ra = a.HalfExtents[i];
            double rb = b.HalfExtents[0] * absR[i, 0] + b.HalfExtents[1] * absR[i, 1] + b.HalfExtents[2] * absR[i, 2];
            if (Math.Abs(Vector.Dot(t, axesA[i])) > ra + rb) return false;
        }

        // Тест 3 осей B
        for (int j = 0; j < 3; j++)
        {
            double ra = a.HalfExtents[0] * absR[0, j] + a.HalfExtents[1] * absR[1, j] + a.HalfExtents[2] * absR[2, j];
            double rb = b.HalfExtents[j];
            if (Math.Abs(Vector.Dot(t, axesB[j])) > ra + rb) return false;
        }

        // Тест 9 осей (попарные векторные произведения)
        // A0 × B0
        if (SeparatedOnCrossAxis(t, axesA, axesB, a.HalfExtents, b.HalfExtents, R, absR, 0, 0)) return false;
        if (SeparatedOnCrossAxis(t, axesA, axesB, a.HalfExtents, b.HalfExtents, R, absR, 0, 1)) return false;
        if (SeparatedOnCrossAxis(t, axesA, axesB, a.HalfExtents, b.HalfExtents, R, absR, 0, 2)) return false;
        if (SeparatedOnCrossAxis(t, axesA, axesB, a.HalfExtents, b.HalfExtents, R, absR, 1, 0)) return false;
        if (SeparatedOnCrossAxis(t, axesA, axesB, a.HalfExtents, b.HalfExtents, R, absR, 1, 1)) return false;
        if (SeparatedOnCrossAxis(t, axesA, axesB, a.HalfExtents, b.HalfExtents, R, absR, 1, 2)) return false;
        if (SeparatedOnCrossAxis(t, axesA, axesB, a.HalfExtents, b.HalfExtents, R, absR, 2, 0)) return false;
        if (SeparatedOnCrossAxis(t, axesA, axesB, a.HalfExtents, b.HalfExtents, R, absR, 2, 1)) return false;
        if (SeparatedOnCrossAxis(t, axesA, axesB, a.HalfExtents, b.HalfExtents, R, absR, 2, 2)) return false;

        return true;
    }

    private static Vector[] GetAxes(Obb obb)
    {
        var axes = new Vector[3];
        for (int i = 0; i < 3; i++)
            axes[i] = new Vector(new[] { obb.Rotation[0, i], obb.Rotation[1, i], obb.Rotation[2, i] });
        return axes;
    }

    private static bool SeparatedOnCrossAxis(Vector t, Vector[] axesA, Vector[] axesB,
        Vector heA, Vector heB, double[,] R, double[,] absR, int i, int j)
    {
        int i1 = (i + 1) % 3, i2 = (i + 2) % 3;
        int j1 = (j + 1) % 3, j2 = (j + 2) % 3;

        double ra = heA[i1] * absR[i2, j] + heA[i2] * absR[i1, j];
        double rb = heB[j1] * absR[i, j2] + heB[j2] * absR[i, j1];

        double tProj = Math.Abs(Vector.Dot(t, axesA[i1]) * R[i2, j] - Vector.Dot(t, axesA[i2]) * R[i1, j]);

        return tProj > ra + rb;
    }
}
