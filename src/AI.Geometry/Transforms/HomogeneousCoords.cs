using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Transforms;

/// <summary>
/// Утилиты для однородных координат.
/// </summary>
public static class HomogeneousCoords
{
    /// <summary>
    /// Преобразует вектор в однородные координаты (добавляет 1 в конец).
    /// </summary>
    public static Vector ToHomogeneous(Vector v)
    {
        var result = new Vector(v.Count + 1);
        for (int i = 0; i < v.Count; i++)
            result[i] = v[i];
        result[v.Count] = 1.0;
        return result;
    }

    /// <summary>
    /// Преобразует однородные координаты обратно в обычные (делит на последнюю компоненту).
    /// </summary>
    public static Vector FromHomogeneous(Vector v)
    {
        int n = v.Count - 1;
        double w = v[n];
        var result = new Vector(n);
        for (int i = 0; i < n; i++)
            result[i] = v[i] / w;
        return result;
    }
}
