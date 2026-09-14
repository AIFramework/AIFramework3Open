#nullable enable
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Симметричная матрица 3×3 на числах: тензор инерции и обратный к нему в горячем цикле решателя. Значимый тип вместо
/// <see cref="Matrix"/>, чтобы произведение на вектор и поворот не выделяли память
/// </summary>
internal readonly record struct SymmetricMatrix3(double Xx, double Xy, double Xz, double Yy, double Yz, double Zz)
{
    /// <summary>Симметричная часть матрицы 3×3</summary>
    public static SymmetricMatrix3 FromMatrix(Matrix m) => new(
        m[0, 0], 0.5 * (m[0, 1] + m[1, 0]), 0.5 * (m[0, 2] + m[2, 0]), m[1, 1], 0.5 * (m[1, 2] + m[2, 1]), m[2, 2]);

    /// <summary>Произведение на вектор</summary>
    public Vector3 Times(Vector3 v) => new(
        (Xx * v.X) + (Xy * v.Y) + (Xz * v.Z),
        (Xy * v.X) + (Yy * v.Y) + (Yz * v.Z),
        (Xz * v.X) + (Yz * v.Y) + (Zz * v.Z));

    /// <summary>
    /// Матрица в повернутых осях R·S·Rᵀ, где столбцы R это оси тела в мировых осях
    /// </summary>
    public SymmetricMatrix3 Rotated(Quaternion orientation)
    {
        var x = orientation.Rotate(new Vector3(1, 0, 0));
        var y = orientation.Rotate(new Vector3(0, 1, 0));
        var z = orientation.Rotate(new Vector3(0, 0, 1));

        // Столбцы произведения R·S
        var c0 = (x * Xx) + (y * Xy) + (z * Xz);
        var c1 = (x * Xy) + (y * Yy) + (z * Yz);
        var c2 = (x * Xz) + (y * Yz) + (z * Zz);
        double Entry(int i, int j) => (c0[i] * x[j]) + (c1[i] * y[j]) + (c2[i] * z[j]);
        return new SymmetricMatrix3(Entry(0, 0), Entry(0, 1), Entry(0, 2), Entry(1, 1), Entry(1, 2), Entry(2, 2));
    }
}
