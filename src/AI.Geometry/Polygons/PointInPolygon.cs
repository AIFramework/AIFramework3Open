using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Проверка принадлежности точки многоугольнику (2D).
/// </summary>
public static class PointInPolygon
{
    /// <summary>
    /// Проверка методом бросания луча (ray casting).
    /// </summary>
    public static bool RayCasting(Vector point, Vector[] polygon)
    {
        int n = polygon.Length;
        bool inside = false;
        double px = point[0], py = point[1];

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = polygon[i][0], yi = polygon[i][1];
            double xj = polygon[j][0], yj = polygon[j][1];

            if (((yi > py) != (yj > py)) &&
                (px < (xj - xi) * (py - yi) / (yj - yi) + xi))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Число обмоток (winding number).
    /// </summary>
    public static int WindingNumber(Vector point, Vector[] polygon)
    {
        int n = polygon.Length;
        int wn = 0;
        double px = point[0], py = point[1];

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double yi = polygon[i][1], yj = polygon[j][1];

            if (yi <= py)
            {
                if (yj > py && IsLeft(polygon[i], polygon[j], point) > 0)
                    wn++;
            }
            else
            {
                if (yj <= py && IsLeft(polygon[i], polygon[j], point) < 0)
                    wn--;
            }
        }

        return wn;
    }

    /// <summary>
    /// Проверяет принадлежность точки многоугольнику (winding number ≠ 0).
    /// </summary>
    public static bool Contains(Vector point, Vector[] polygon)
    {
        return WindingNumber(point, polygon) != 0;
    }

    private static double IsLeft(Vector a, Vector b, Vector p)
    {
        return (b[0] - a[0]) * (p[1] - a[1]) - (p[0] - a[0]) * (b[1] - a[1]);
    }
}
