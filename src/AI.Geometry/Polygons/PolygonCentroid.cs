using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Центроид (центр масс) многоугольника.
/// </summary>
public static class PolygonCentroid
{
    /// <summary>
    /// Вычисляет центроид простого многоугольника (2D).
    /// </summary>
    public static Vector Centroid(Vector[] polygon)
    {
        int n = polygon.Length;
        double cx = 0, cy = 0;
        double area = 0;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double cross = polygon[i][0] * polygon[j][1] - polygon[j][0] * polygon[i][1];
            area += cross;
            cx += (polygon[i][0] + polygon[j][0]) * cross;
            cy += (polygon[i][1] + polygon[j][1]) * cross;
        }

        area *= 0.5;
        double factor = 1.0 / (6.0 * area);
        return new Vector(new[] { cx * factor, cy * factor });
    }
}
