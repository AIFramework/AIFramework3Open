#nullable enable

using System;
using AI.Geometry.Primitives;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.Transforms;

/// <summary>
/// Движение твердого тела в пространстве (элемент группы SE(3)): поворот единичным кватернионом и перенос.
/// Точка p переходит в R·p + t.
/// </summary>
/// <remarks>
/// Экспонента и логарифм связывают движение с твистом (ω, v): ω это вектор поворота, v линейная часть.
/// Перенос равен V·v, где V = I + (1 − cos θ)/θ²·[ω]× + (θ − sin θ)/θ³·[ω]×², θ = |ω| (формула Родрига).
/// При малом угле коэффициенты берутся рядами Тейлора, поэтому нулевой и почти нулевой поворот не теряют точность.
/// </remarks>
/// <param name="Rotation">Поворот, единичный кватернион.</param>
/// <param name="Translation">Перенос.</param>
public readonly record struct RigidTransform(Quaternion Rotation, Vector3 Translation)
{
    // Ниже этого угла коэффициенты V и V⁻¹ считаются рядами: прямые формулы теряют точность как ε/θ²
    private const double SeriesAngle = 1e-2;

    /// <summary>
    /// Тождественное движение.
    /// </summary>
    public static RigidTransform Identity => new(Quaternion.Identity, Vector3.Zero);

    /// <summary>
    /// Обратное движение: R⁻¹ и −R⁻¹·t.
    /// </summary>
    public RigidTransform Inverse
    {
        get
        {
            Quaternion back = Rotation.Conjugate;
            return new RigidTransform(back, -back.Rotate(Translation));
        }
    }

    /// <summary>
    /// Композиция: сначала <paramref name="second"/>, затем <paramref name="first"/>.
    /// </summary>
    /// <param name="first">Движение, выполняемое вторым.</param>
    /// <param name="second">Движение, выполняемое первым.</param>
    public static RigidTransform operator *(RigidTransform first, RigidTransform second) =>
        new(first.Rotation * second.Rotation, first.Rotation.Rotate(second.Translation) + first.Translation);

    /// <summary>
    /// Поворот вокруг прямой на угол по правилу правой руки: большой палец по направлению прямой.
    /// </summary>
    /// <param name="point">Точка на оси.</param>
    /// <param name="direction">Направление оси, не обязательно единичное.</param>
    /// <param name="angle">Угол, радианы.</param>
    public static RigidTransform AboutAxis(Vector3 point, Vector3 direction, double angle)
    {
        Quaternion rotation = Quaternion.FromAxisAngle(direction, angle);
        return new RigidTransform(rotation, point - rotation.Rotate(point));
    }

    /// <summary>
    /// Экспоненциальное отображение: движение по твисту (ω, v).
    /// </summary>
    /// <param name="angular">Вектор поворота ω: ось, умноженная на угол в радианах.</param>
    /// <param name="linear">Линейная часть твиста v.</param>
    public static RigidTransform Exp(Vector3 angular, Vector3 linear)
    {
        double angle = angular.Length;
        double squared = angle * angle;
        double a;
        double b;

        if (angle < SeriesAngle)
        {
            // (1 − cos θ)/θ² = 1/2 − θ²/24 + θ⁴/720, (θ − sin θ)/θ³ = 1/6 − θ²/120 + θ⁴/5040
            a = 0.5 - (squared / 24) + (squared * squared / 720);
            b = (1.0 / 6) - (squared / 120) + (squared * squared / 5040);
        }
        else
        {
            a = (1 - Math.Cos(angle)) / squared;
            b = (angle - Math.Sin(angle)) / (squared * angle);
        }

        Vector3 turn = angular.Cross(linear);
        Vector3 translation = linear + (turn * a) + (angular.Cross(turn) * b);
        return new RigidTransform(Quaternion.FromRotationVector(angular), translation);
    }

    /// <summary>
    /// Логарифмическое отображение: твист (ω, v), для которого <see cref="Exp"/> дает это движение.
    /// Угол поворота берется не больше π.
    /// </summary>
    /// <returns>Вектор поворота и линейная часть твиста.</returns>
    public (Vector3 Angular, Vector3 Linear) Log()
    {
        Vector3 angular = Rotation.ToRotationVector();
        double angle = angular.Length;
        double squared = angle * angle;

        // V⁻¹ = I − ½·[ω]× + c·[ω]×², c = (1 − (θ/2)·ctg(θ/2))/θ² = 1/12 + θ²/720 + θ⁴/30240 + ...
        double c = angle < SeriesAngle
            ? (1.0 / 12) + (squared / 720) + (squared * squared / 30240)
            : (1 - (0.5 * angle / Math.Tan(0.5 * angle))) / squared;

        Vector3 turn = angular.Cross(Translation);
        return (angular, Translation - (turn * 0.5) + (angular.Cross(turn) * c));
    }

    /// <summary>
    /// Винтовая интерполяция: движение по винту от <paramref name="from"/> к <paramref name="to"/>,
    /// from·exp(s·log(from⁻¹·to)). При s = 0 дает from, при s = 1 дает to.
    /// </summary>
    /// <param name="from">Движение при s = 0.</param>
    /// <param name="to">Движение при s = 1.</param>
    /// <param name="s">Доля пути; вне отрезка [0, 1] дает экстраполяцию.</param>
    public static RigidTransform Interpolate(RigidTransform from, RigidTransform to, double s)
    {
        var (angular, linear) = (from.Inverse * to).Log();
        return from * Exp(angular * s, linear * s);
    }

    /// <summary>
    /// Образ точки: R·p + t.
    /// </summary>
    /// <param name="point">Точка.</param>
    public Vector3 Apply(Vector3 point) => Rotation.Rotate(point) + Translation;

    /// <summary>
    /// Образ вектора (направления): только поворот.
    /// </summary>
    /// <param name="vector">Вектор.</param>
    public Vector3 ApplyVector(Vector3 vector) => Rotation.Rotate(vector);

    /// <summary>
    /// То же движение однородной матрицей 4×4.
    /// </summary>
    public Affine3D ToAffine()
    {
        Matrix m = Affine3D.FromQuaternion(Rotation).M;
        m[0, 3] = Translation.X;
        m[1, 3] = Translation.Y;
        m[2, 3] = Translation.Z;
        return new Affine3D(m);
    }
}
