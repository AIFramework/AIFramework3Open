using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Площадь многоугольника по формуле шнурка (Shoelace / Gauss).
/// </summary>
public static class ShoelaceArea
{
    /// <summary>
    /// Знаковая площадь многоугольника (положительная для CCW, отрицательная для CW).
    /// </summary>
    public static double SignedArea(Vector[] polygon)
    {
        int n = polygon.Length;
        double area = 0;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += polygon[i][0] * polygon[j][1];
            area -= polygon[j][0] * polygon[i][1];
        }
        return area * 0.5;
    }

    /// <summary>
    /// Площадь многоугольника (абсолютное значение).
    /// </summary>
    public static double Area(Vector[] polygon)
    {
        return Math.Abs(SignedArea(polygon));
    }
}
