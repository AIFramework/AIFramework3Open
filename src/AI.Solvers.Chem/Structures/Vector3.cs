using AI.DataStructs.Algebraic;
using System.Globalization;

namespace AI.Solvers.Chem.Structures;

/// <summary>
/// Точка или вектор в трёхмерном пространстве
/// </summary>
/// <param name="X">Координата X</param>
/// <param name="Y">Координата Y</param>
/// <param name="Z">Координата Z</param>
/// <remarks>
/// Значимый тип, а не <see cref="Vector"/> фреймворка: координат атомов бывают
/// десятки тысяч на кадр траектории, и размещать под каждую точку список в куче
/// слишком дорого. Для алгоритмов <c>AI.Geometry</c> есть <see cref="ToVector"/>.
/// </remarks>
public readonly record struct Vector3(double X, double Y, double Z)
{
    /// <summary>Нулевой вектор</summary>
    public static Vector3 Zero => new(0, 0, 0);

    /// <summary>Сумма</summary>
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Разность</summary>
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>Смена знака</summary>
    public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);

    /// <summary>Умножение на число</summary>
    public static Vector3 operator *(Vector3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    /// <summary>Умножение на число</summary>
    public static Vector3 operator *(double k, Vector3 a) => a * k;

    /// <summary>Деление на число</summary>
    public static Vector3 operator /(Vector3 a, double k) => new(a.X / k, a.Y / k, a.Z / k);

    /// <summary>Скалярное произведение</summary>
    public double Dot(Vector3 other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

    /// <summary>Векторное произведение</summary>
    public Vector3 Cross(Vector3 other) => new(
        (Y * other.Z) - (Z * other.Y),
        (Z * other.X) - (X * other.Z),
        (X * other.Y) - (Y * other.X));

    /// <summary>Длина вектора</summary>
    public double Length => Math.Sqrt(Dot(this));

    /// <summary>Квадрат длины</summary>
    public double LengthSquared => Dot(this);

    /// <summary>Единичный вектор того же направления</summary>
    public Vector3 Normalized
    {
        get
        {
            double length = Length;
            return length > 0 ? this / length : Zero;
        }
    }

    /// <summary>Расстояние до другой точки</summary>
    public double DistanceTo(Vector3 other) => (this - other).Length;

    /// <summary>Угол с другим вектором, градусы</summary>
    public double AngleTo(Vector3 other)
    {
        double lengths = Length * other.Length;

        if (lengths <= 0)
            return double.NaN;

        return Math.Acos(Math.Clamp(Dot(other) / lengths, -1, 1)) * 180 / Math.PI;
    }

    /// <summary>Покомпонентное произведение</summary>
    public Vector3 Scale(Vector3 other) => new(X * other.X, Y * other.Y, Z * other.Z);

    /// <summary>Координата по индексу</summary>
    public double this[int index] => index switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(index), "У точки три координаты")
    };

    /// <summary>Представление вектором фреймворка (для алгоритмов AI.Geometry)</summary>
    public Vector ToVector() => new(X, Y, Z);

    /// <summary>Создаёт точку из вектора фреймворка</summary>
    /// <param name="vector">Вектор длины 3</param>
    public static Vector3 FromVector(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (vector.Count != 3)
            throw new ArgumentException("Ожидается вектор длины 3", nameof(vector));

        return new Vector3(vector[0], vector[1], vector[2]);
    }

    /// <summary>Координаты через пробел</summary>
    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture, "{0:F6} {1:F6} {2:F6}", X, Y, Z);
}
