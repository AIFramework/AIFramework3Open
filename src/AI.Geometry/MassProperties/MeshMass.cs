#nullable enable

using System;
using System.Collections.Generic;
using AI.Geometry.Primitives;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.MassProperties;

/// <summary>
/// Объем, центр масс и тензор инерции тела, ограниченного замкнутой треугольной сеткой.
/// </summary>
/// <remarks>
/// Объемные интегралы от 1, x, y, z, x², y², z², xy, yz, zx сводятся теоремой Гаусса – Остроградского
/// к сумме по граням, а по каждому треугольнику считаются в замкнутом виде (Эберли, «Polyhedral Mass Properties»).
/// Перед суммированием вершины сдвигаются к их среднему: так интегралы второго порядка не теряют точность
/// у сетки, удаленной от начала координат.
/// </remarks>
public static class MeshMass
{
    /// <summary>
    /// Массовые характеристики тела единичной плотности.
    /// </summary>
    /// <param name="vertices">Вершины сетки.</param>
    /// <param name="triangles">Треугольники как тройки индексов вершин. Сетка должна быть замкнутой, а обход
    /// всех треугольников одинаковым: против часовой стрелки при взгляде снаружи. Сетка, у которой все
    /// треугольники обходятся по часовой стрелке, тоже допустима: знак объема при этом исправляется.</param>
    /// <exception cref="ArgumentException">Сетка не ограничивает ненулевой объем.</exception>
    public static SolidProperties Compute(IReadOnlyList<Vector3> vertices, IReadOnlyList<(int A, int B, int C)> triangles)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(triangles);

        if (vertices.Count == 0 || triangles.Count == 0)
            throw new ArgumentException("Сетка пуста", nameof(triangles));

        Vector3 origin = Vector3.Zero;

        foreach (Vector3 v in vertices)
            origin += v;

        origin /= vertices.Count;

        // Интегралы 1, x, y, z, x², y², z², xy, yz, zx без общих множителей
        double i1 = 0, ix = 0, iy = 0, iz = 0, ixx = 0, iyy = 0, izz = 0, ixy = 0, iyz = 0, izx = 0;
        double scale = 0;

        foreach (var (a, b, c) in triangles)
        {
            Vector3 p0 = vertices[a] - origin;
            Vector3 p1 = vertices[b] - origin;
            Vector3 p2 = vertices[c] - origin;
            Vector3 d = (p1 - p0).Cross(p2 - p0);

            var x = Subexpressions(p0.X, p1.X, p2.X);
            var y = Subexpressions(p0.Y, p1.Y, p2.Y);
            var z = Subexpressions(p0.Z, p1.Z, p2.Z);

            i1 += d.X * x.F1;
            ix += d.X * x.F2;
            iy += d.Y * y.F2;
            iz += d.Z * z.F2;
            ixx += d.X * x.F3;
            iyy += d.Y * y.F3;
            izz += d.Z * z.F3;
            ixy += d.X * ((p0.Y * x.G0) + (p1.Y * x.G1) + (p2.Y * x.G2));
            iyz += d.Y * ((p0.Z * y.G0) + (p1.Z * y.G1) + (p2.Z * y.G2));
            izx += d.Z * ((p0.X * z.G0) + (p1.X * z.G1) + (p2.X * z.G2));
            scale = Math.Max(scale, Math.Max(p0.Length, Math.Max(p1.Length, p2.Length)));
        }

        double volume = i1 / 6;

        if (Math.Abs(volume) <= 1e-12 * scale * scale * scale)
            throw new ArgumentException("Сетка не ограничивает объем: она не замкнута либо вырождена", nameof(triangles));

        // Обход по часовой стрелке меняет знак всех интегралов сразу
        double sign = Math.Sign(volume);
        volume *= sign;
        var centroid = new Vector3(ix, iy, iz) * (sign / 24 / volume);
        ixx *= sign / 60;
        iyy *= sign / 60;
        izz *= sign / 60;
        ixy *= sign / 120;
        iyz *= sign / 120;
        izx *= sign / 120;

        // Моменты относительно центра масс по теореме Штейнера
        double cx = centroid.X, cy = centroid.Y, cz = centroid.Z;
        double xx = ixx - (volume * cx * cx);
        double yy = iyy - (volume * cy * cy);
        double zz = izz - (volume * cz * cz);
        double xy = ixy - (volume * cx * cy);
        double yz = iyz - (volume * cy * cz);
        double zx = izx - (volume * cz * cx);

        var inertia = new Matrix(3, 3);
        inertia[0, 0] = yy + zz;
        inertia[1, 1] = zz + xx;
        inertia[2, 2] = xx + yy;
        inertia[0, 1] = inertia[1, 0] = -xy;
        inertia[1, 2] = inertia[2, 1] = -yz;
        inertia[0, 2] = inertia[2, 0] = -zx;

        return new SolidProperties(volume, origin + centroid, inertia);
    }

    /// <summary>
    /// Вспомогательные многочлены Эберли для одной координаты вершин треугольника.
    /// </summary>
    private static (double F1, double F2, double F3, double G0, double G1, double G2) Subexpressions(double w0, double w1, double w2)
    {
        double temp0 = w0 + w1;
        double f1 = temp0 + w2;
        double temp1 = w0 * w0;
        double temp2 = temp1 + (w1 * temp0);
        double f2 = temp2 + (w2 * f1);
        double f3 = (w0 * temp1) + (w1 * temp2) + (w2 * f2);
        return (f1, f2, f3, f2 + (w0 * (f1 + w0)), f2 + (w1 * (f1 + w1)), f2 + (w2 * (f1 + w2)));
    }
}
