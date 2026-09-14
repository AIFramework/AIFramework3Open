#nullable enable

using System;
using AI.ClassicMath.MatrixUtils;
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.MassProperties;

/// <summary>
/// Тензоры инерции простых тел в замкнутом виде и действия над тензором: перенос, поворот, главные оси.
/// </summary>
/// <remarks>
/// Все тензоры представлены симметричными матрицами 3×3 относительно центра масс тела в его собственных осях.
/// У цилиндра и капсулы ось симметрии направлена по Z.
/// </remarks>
public static class InertiaTensor
{
    /// <summary>
    /// Диагональный тензор с заданными моментами.
    /// </summary>
    /// <param name="moments">Моменты относительно осей X, Y, Z.</param>
    public static Matrix FromDiagonal(Vector3 moments)
    {
        var tensor = new Matrix(3, 3);
        tensor[0, 0] = moments.X;
        tensor[1, 1] = moments.Y;
        tensor[2, 2] = moments.Z;
        return tensor;
    }

    /// <summary>
    /// Сплошной прямоугольный параллелепипед.
    /// </summary>
    /// <param name="mass">Масса.</param>
    /// <param name="halfExtents">Полуразмеры по осям.</param>
    public static Matrix Box(double mass, Vector3 halfExtents)
    {
        double x2 = halfExtents.X * halfExtents.X;
        double y2 = halfExtents.Y * halfExtents.Y;
        double z2 = halfExtents.Z * halfExtents.Z;
        return FromDiagonal(new Vector3(y2 + z2, z2 + x2, x2 + y2) * (mass / 3));
    }

    /// <summary>
    /// Сплошной шар: 2/5·m·r² относительно любой оси.
    /// </summary>
    /// <param name="mass">Масса.</param>
    /// <param name="radius">Радиус.</param>
    public static Matrix Sphere(double mass, double radius)
    {
        double moment = 0.4 * mass * radius * radius;
        return FromDiagonal(new Vector3(moment, moment, moment));
    }

    /// <summary>
    /// Сплошной круговой цилиндр с осью Z.
    /// </summary>
    /// <param name="mass">Масса.</param>
    /// <param name="radius">Радиус основания.</param>
    /// <param name="height">Полная высота.</param>
    public static Matrix Cylinder(double mass, double radius, double height)
    {
        double r2 = radius * radius;
        double side = mass * ((3 * r2) + (height * height)) / 12;
        return FromDiagonal(new Vector3(side, side, 0.5 * mass * r2));
    }

    /// <summary>
    /// Сплошная капсула с осью Z: цилиндр, закрытый двумя полушариями того же радиуса.
    /// </summary>
    /// <remarks>
    /// Масса делится между цилиндром и полушариями пропорционально объемам. Момент полушария
    /// относительно поперечной оси через его центр масс равен 83/320·m·r², центр масс удален от
    /// плоского основания на 3/8·r; перенос к центру капсулы дает слагаемое 1/4·h² + 3/8·h·r.
    /// </remarks>
    /// <param name="mass">Масса.</param>
    /// <param name="radius">Радиус цилиндра и полушарий.</param>
    /// <param name="height">Высота цилиндрической части (без полушарий).</param>
    public static Matrix Capsule(double mass, double radius, double height)
    {
        double r2 = radius * radius;
        double cylinderVolume = Math.PI * r2 * height;
        double sphereVolume = 4.0 / 3.0 * Math.PI * r2 * radius;
        double cylinderMass = mass * cylinderVolume / (cylinderVolume + sphereVolume);
        double sphereMass = mass - cylinderMass;

        double axial = (0.5 * cylinderMass * r2) + (0.4 * sphereMass * r2);
        double side = (cylinderMass * ((height * height / 12) + (r2 / 4)))
            + (sphereMass * ((0.4 * r2) + (height * height / 4) + (0.375 * height * radius)));
        return FromDiagonal(new Vector3(side, side, axial));
    }

    /// <summary>
    /// Перенос тензора от центра масс к другой точке (теорема Штейнера): I + m·(|d|²·E − d·dᵀ).
    /// </summary>
    /// <param name="inertiaAboutCentroid">Тензор относительно центра масс.</param>
    /// <param name="mass">Масса тела.</param>
    /// <param name="offset">Смещение новой точки относительно центра масс.</param>
    public static Matrix ParallelAxis(Matrix inertiaAboutCentroid, double mass, Vector3 offset)
    {
        ArgumentNullException.ThrowIfNull(inertiaAboutCentroid);

        var result = new Matrix(3, 3);
        double squared = offset.LengthSquared;

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
                result[i, j] = inertiaAboutCentroid[i, j] + (mass * (((i == j) ? squared : 0) - (offset[i] * offset[j])));
        }

        return result;
    }

    /// <summary>
    /// Тензор в повернутых осях: R·I·Rᵀ.
    /// </summary>
    /// <param name="inertia">Тензор в собственных осях тела.</param>
    /// <param name="rotation">Матрица поворота 3×3 из осей тела в мировые.</param>
    public static Matrix Rotate(Matrix inertia, Matrix rotation)
    {
        ArgumentNullException.ThrowIfNull(inertia);
        ArgumentNullException.ThrowIfNull(rotation);

        var result = new Matrix(3, 3);

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double sum = 0;

                for (int k = 0; k < 3; k++)
                {
                    for (int l = 0; l < 3; l++)
                        sum += rotation[i, k] * inertia[k, l] * rotation[j, l];
                }

                result[i, j] = sum;
            }
        }

        return result;
    }

    /// <summary>
    /// Тензор в повернутых осях: R·I·Rᵀ, где поворот R задан кватернионом.
    /// </summary>
    /// <remarks>
    /// Так из диагонального тензора тела и его ориентации получается тензор в мировых осях;
    /// обратный тензор в мировых осях получается поворотом диагонали из обратных моментов.
    /// </remarks>
    /// <param name="inertia">Тензор в собственных осях тела.</param>
    /// <param name="orientation">Единичный кватернион: поворот из осей тела в мировые.</param>
    public static Matrix Rotate(Matrix inertia, Quaternion orientation)
        => Rotate(inertia, orientation.ToRotationMatrix3());

    /// <summary>
    /// Произведение тензора на вектор, например момент импульса I·ω.
    /// </summary>
    /// <param name="tensor">Матрица 3×3.</param>
    /// <param name="vector">Вектор.</param>
    public static Vector3 Apply(Matrix tensor, Vector3 vector)
    {
        ArgumentNullException.ThrowIfNull(tensor);

        return new Vector3(
            (tensor[0, 0] * vector.X) + (tensor[0, 1] * vector.Y) + (tensor[0, 2] * vector.Z),
            (tensor[1, 0] * vector.X) + (tensor[1, 1] * vector.Y) + (tensor[1, 2] * vector.Z),
            (tensor[2, 0] * vector.X) + (tensor[2, 1] * vector.Y) + (tensor[2, 2] * vector.Z));
    }

    /// <summary>
    /// Главные моменты и главные оси тензора.
    /// </summary>
    /// <param name="inertia">Симметричный тензор 3×3.</param>
    /// <returns>Моменты по возрастанию и матрица поворота, в столбцах которой лежат соответствующие оси;
    /// тройка осей правая, так что Axes·diag(Moments)·Axesᵀ равно исходному тензору.</returns>
    public static (Vector3 Moments, Matrix Axes) Principal(Matrix inertia)
    {
        ArgumentNullException.ThrowIfNull(inertia);

        var (values, vectors) = Eigen.Symmetric(inertia, EigenOrder.Ascending);
        var first = new Vector3(vectors[0, 0], vectors[1, 0], vectors[2, 0]);
        var second = new Vector3(vectors[0, 1], vectors[1, 1], vectors[2, 1]);
        var third = new Vector3(vectors[0, 2], vectors[1, 2], vectors[2, 2]);

        if (first.Cross(second).Dot(third) < 0)
        {
            for (int i = 0; i < 3; i++)
                vectors[i, 2] = -vectors[i, 2];
        }

        return (new Vector3(values[0], values[1], values[2]), vectors);
    }
}
