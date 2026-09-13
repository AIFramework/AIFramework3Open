using System.Globalization;
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Geometry.Primitives;
using AI.Units;

namespace AI.Physics.Continuum;

/// <summary>Главные значения тензора по убыванию и их направления</summary>
/// <param name="First">Наибольшее главное значение</param>
/// <param name="Second">Среднее</param>
/// <param name="Third">Наименьшее</param>
/// <param name="FirstDirection">Единичный вектор первого главного направления</param>
/// <param name="SecondDirection">Второго</param>
/// <param name="ThirdDirection">Третьего</param>
public readonly record struct PrincipalValues(
    Quantity First, Quantity Second, Quantity Third,
    Vector3 FirstDirection, Vector3 SecondDirection, Vector3 ThirdDirection);

/// <summary>Круг Мора: центр и радиус</summary>
/// <param name="Center">Центр — полусумма двух главных значений</param>
/// <param name="Radius">Радиус — их полуразность, наибольшее касательное на площадках между ними</param>
public readonly record struct MohrCircle(Quantity Center, Quantity Radius);

/// <summary>Вектор напряжения на площадке и его составляющие</summary>
/// <param name="Vector">Вектор напряжения в единицах СИ</param>
/// <param name="Normal">Нормальная составляющая: растяжение положительно</param>
/// <param name="Shear">Модуль касательной составляющей</param>
public readonly record struct PlaneTraction(Vector3 Vector, Quantity Normal, Quantity Shear);

/// <summary>
/// Симметричный тензор второго ранга в трёхмерном пространстве — напряжения или деформации.
/// </summary>
/// <remarks>
/// <para>
/// Растяжение положительно, сжатие отрицательно: гидростатическое давление p — это тензор −p·I.
/// Касательные компоненты деформации тензорные, ε_xy = γ_xy/2: инженерный сдвиг γ вдвое больше,
/// и подставить его вместо тензорной компоненты — частая ошибка вдвое.
/// </para>
/// <para>
/// Главные значения и направления находит общий метод вращений Якоби из <c>AI.ClassicMath</c>;
/// второй реализации здесь нет. Инварианты не зависят от выбора осей — это и есть способ
/// проверить, что поворот выполнен верно.
/// </para>
/// </remarks>
public sealed class SymmetricTensor : IEquatable<SymmetricTensor>
{
    private readonly double[] _c;

    private SymmetricTensor(double xx, double yy, double zz, double xy, double yz, double zx, Dimension dimension)
    {
        _c = [xx, yy, zz, xy, yz, zx];

        if (_c.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
            throw new ArgumentException("Компоненты тензора должны быть конечными числами");

        Dimension = dimension;
    }

    /// <summary>Размерность компонент: давление для напряжений, безразмерная для деформаций</summary>
    public Dimension Dimension { get; }

    /// <summary>Тензор напряжений по компонентам</summary>
    /// <param name="xx">σxx</param>
    /// <param name="yy">σyy</param>
    /// <param name="zz">σzz</param>
    /// <param name="xy">τxy</param>
    /// <param name="yz">τyz</param>
    /// <param name="zx">τzx</param>
    public static SymmetricTensor Stress(
        Quantity xx, Quantity yy = default, Quantity zz = default,
        Quantity xy = default, Quantity yz = default, Quantity zx = default)
        => new(
            StressComponent(xx, nameof(xx)), StressComponent(yy, nameof(yy)), StressComponent(zz, nameof(zz)),
            StressComponent(xy, nameof(xy)), StressComponent(yz, nameof(yz)), StressComponent(zx, nameof(zx)),
            Dimension.Pressure);

    /// <summary>Тензор малых деформаций; касательные компоненты — тензорные, половина инженерного сдвига</summary>
    /// <param name="xx">εxx</param>
    /// <param name="yy">εyy</param>
    /// <param name="zz">εzz</param>
    /// <param name="xy">εxy = γxy/2</param>
    /// <param name="yz">εyz = γyz/2</param>
    /// <param name="zx">εzx = γzx/2</param>
    public static SymmetricTensor Strain(double xx, double yy = 0, double zz = 0, double xy = 0, double yz = 0, double zx = 0)
        => new(xx, yy, zz, xy, yz, zx, Dimension.None);

    /// <summary>Одноосное растяжение вдоль x; сжатие — отрицательным значением</summary>
    /// <param name="stress">Напряжение</param>
    public static SymmetricTensor Uniaxial(Quantity stress) => Stress(stress);

    /// <summary>Чистый сдвиг в плоскости xy</summary>
    /// <param name="shear">Касательное напряжение</param>
    public static SymmetricTensor PureShear(Quantity shear) => Stress(default, default, default, shear);

    /// <summary>Всестороннее давление: тензор −p·I</summary>
    /// <param name="pressure">Давление</param>
    public static SymmetricTensor Hydrostatic(Quantity pressure)
    {
        double p = pressure.RequireSi(Dimension.Pressure, nameof(pressure));

        return new SymmetricTensor(-p, -p, -p, 0, 0, 0, Dimension.Pressure);
    }

    /// <summary>Тензор из матрицы 3×3 в единицах СИ</summary>
    /// <param name="components">Симметричная матрица</param>
    /// <param name="dimension">Размерность компонент</param>
    public static SymmetricTensor FromMatrix(double[,] components, Dimension dimension)
    {
        ArgumentNullException.ThrowIfNull(components);

        if (components.GetLength(0) != 3 || components.GetLength(1) != 3)
            throw new ArgumentException("Нужна матрица 3×3", nameof(components));

        double scale = 1e-12 * Math.Max(1, components.Cast<double>().Max(Math.Abs));

        for (int i = 0; i < 3; i++)
        {
            for (int j = i + 1; j < 3; j++)
            {
                if (Math.Abs(components[i, j] - components[j, i]) > scale)
                    throw new ArgumentException($"Матрица несимметрична в позиции ({i}, {j})", nameof(components));
            }
        }

        return new SymmetricTensor(
            components[0, 0], components[1, 1], components[2, 2],
            components[0, 1], components[1, 2], components[0, 2], dimension);
    }

    /// <summary>Компонента в единицах СИ</summary>
    /// <param name="i">Строка, 0 — x</param>
    /// <param name="j">Столбец</param>
    public double this[int i, int j]
    {
        get
        {
            if ((uint)i > 2 || (uint)j > 2)
                throw new ArgumentOutOfRangeException(i > 2 ? nameof(i) : nameof(j), "Индексы тензора — 0, 1, 2");

            if (i == j)
                return _c[i];

            return (Math.Min(i, j), Math.Max(i, j)) switch
            {
                (0, 1) => _c[3],
                (1, 2) => _c[4],
                _ => _c[5]
            };
        }
    }

    /// <summary>Компонента xx</summary>
    public Quantity Xx => Q(_c[0]);

    /// <summary>Компонента yy</summary>
    public Quantity Yy => Q(_c[1]);

    /// <summary>Компонента zz</summary>
    public Quantity Zz => Q(_c[2]);

    /// <summary>Компонента xy</summary>
    public Quantity Xy => Q(_c[3]);

    /// <summary>Компонента yz</summary>
    public Quantity Yz => Q(_c[4]);

    /// <summary>Компонента zx</summary>
    public Quantity Zx => Q(_c[5]);

    /// <summary>След — первый инвариант I₁</summary>
    public Quantity Trace => Q(_c[0] + _c[1] + _c[2]);

    /// <summary>Среднее нормальное: для напряжений — минус давление, для деформаций — треть объёмной</summary>
    public Quantity Mean => Q((_c[0] + _c[1] + _c[2]) / 3);

    /// <summary>Девиатор: тензор без шаровой части</summary>
    public SymmetricTensor Deviator
    {
        get
        {
            double mean = (_c[0] + _c[1] + _c[2]) / 3;

            return new SymmetricTensor(_c[0] - mean, _c[1] - mean, _c[2] - mean, _c[3], _c[4], _c[5], Dimension);
        }
    }

    /// <summary>Второй инвариант I₂ — сумма главных миноров</summary>
    public Quantity SecondInvariant
        => new((_c[0] * _c[1]) + (_c[1] * _c[2]) + (_c[2] * _c[0]) - (_c[3] * _c[3]) - (_c[4] * _c[4]) - (_c[5] * _c[5]),
            Dimension * Dimension);

    /// <summary>Третий инвариант I₃ — определитель</summary>
    public Quantity ThirdInvariant => new(Determinant(_c), Dimension * Dimension * Dimension);

    /// <summary>Второй инвариант девиатора J₂ = ½·s:s</summary>
    public Quantity J2 => new(J2Si, Dimension * Dimension);

    /// <summary>Третий инвариант девиатора J₃ = det s</summary>
    public Quantity J3 => new(Determinant(Deviator._c), Dimension * Dimension * Dimension);

    /// <summary>
    /// Эквивалентное напряжение по Мизесу √(3·J₂): одноосное напряжение с той же энергией формоизменения
    /// </summary>
    public Quantity VonMises => Q(Math.Sqrt(3 * J2Si));

    /// <summary>Октаэдрическое касательное √(2·J₂/3)</summary>
    public Quantity OctahedralShear => Q(Math.Sqrt(2 * J2Si / 3));

    /// <summary>Наибольшее касательное — радиус большого круга Мора (σ₁ − σ₃)/2</summary>
    public Quantity MaxShear
    {
        get
        {
            PrincipalValues p = Principal();

            return Q((p.First.SiValue - p.Third.SiValue) / 2);
        }
    }

    /// <summary>
    /// Угол Лоде, градусы: 0° — одноосное растяжение, 30° — чистый сдвиг, 60° — одноосное сжатие;
    /// NaN для шарового тензора
    /// </summary>
    public double LodeAngleDegrees
    {
        get
        {
            double j2 = J2Si;

            if (j2 <= 1e-30 * Math.Max(1, _c.Max(Math.Abs) * _c.Max(Math.Abs)))
                return double.NaN;

            double cosine = 3 * Math.Sqrt(3) / 2 * Determinant(Deviator._c) / Math.Pow(j2, 1.5);

            return Math.Acos(Math.Clamp(cosine, -1, 1)) / 3 * 180 / Math.PI;
        }
    }

    /// <summary>Главные значения по убыванию и главные направления</summary>
    public PrincipalValues Principal()
    {
        var matrix = new Matrix(3, 3);

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                matrix[i, j] = this[i, j];

        (Vector values, Matrix vectors) = Eigen.Symmetric(matrix, EigenOrder.Descending);

        Vector3 Column(int k) => new Vector3(vectors[0, k], vectors[1, k], vectors[2, k]).Normalized;

        return new PrincipalValues(Q(values[0]), Q(values[1]), Q(values[2]), Column(0), Column(1), Column(2));
    }

    /// <summary>Три круга Мора: для пар главных значений (1, 2), (2, 3) и (1, 3)</summary>
    public IReadOnlyList<MohrCircle> MohrCircles()
    {
        PrincipalValues p = Principal();
        double s1 = p.First.SiValue, s2 = p.Second.SiValue, s3 = p.Third.SiValue;

        return
        [
            new MohrCircle(Q((s1 + s2) / 2), Q((s1 - s2) / 2)),
            new MohrCircle(Q((s2 + s3) / 2), Q((s2 - s3) / 2)),
            new MohrCircle(Q((s1 + s3) / 2), Q((s1 - s3) / 2))
        ];
    }

    /// <summary>Вектор напряжения на площадке по формуле Коши t = σ·n</summary>
    /// <param name="normal">Нормаль площадки; нормируется</param>
    public PlaneTraction Traction(Vector3 normal)
    {
        double length = normal.Length;

        if (!(length > 0))
            throw new ArgumentException("Нормаль площадки не может быть нулевой", nameof(normal));

        Vector3 n = normal / length;
        Vector3 t = Apply(n);
        double normalPart = t.Dot(n);
        double shear = (t - (n * normalPart)).Length;

        return new PlaneTraction(t, Q(normalPart), Q(shear));
    }

    /// <summary>Компоненты в другом ортонормированном базисе: σ′ᵢⱼ = eᵢ·σ·eⱼ</summary>
    /// <param name="e1">Первый базисный вектор</param>
    /// <param name="e2">Второй</param>
    /// <param name="e3">Третий</param>
    public SymmetricTensor InBasis(Vector3 e1, Vector3 e2, Vector3 e3)
    {
        Vector3[] basis = [e1, e2, e3];

        for (int i = 0; i < 3; i++)
        {
            if (Math.Abs(basis[i].Length - 1) > 1e-9)
                throw new ArgumentException("Базис должен быть ортонормированным: вектор не единичный");

            for (int j = i + 1; j < 3; j++)
            {
                if (Math.Abs(basis[i].Dot(basis[j])) > 1e-9)
                    throw new ArgumentException("Базис должен быть ортонормированным: векторы не перпендикулярны");
            }
        }

        double Component(int i, int j) => basis[i].Dot(Apply(basis[j]));

        return new SymmetricTensor(
            Component(0, 0), Component(1, 1), Component(2, 2),
            Component(0, 1), Component(1, 2), Component(0, 2), Dimension);
    }

    /// <summary>Компоненты в осях, повёрнутых вокруг z на заданный угол против часовой стрелки</summary>
    /// <param name="angleDegrees">Угол поворота осей, градусы</param>
    public SymmetricTensor RotatedAboutZ(double angleDegrees)
    {
        double a = angleDegrees * Math.PI / 180;

        return InBasis(new Vector3(Math.Cos(a), Math.Sin(a), 0), new Vector3(-Math.Sin(a), Math.Cos(a), 0), new Vector3(0, 0, 1));
    }

    /// <summary>Двойная свёртка Σ aᵢⱼ·bᵢⱼ: например σ:ε — удвоенная удельная энергия деформации</summary>
    /// <param name="other">Второй тензор</param>
    public Quantity DoubleContraction(SymmetricTensor other)
    {
        ArgumentNullException.ThrowIfNull(other);

        double sum = (_c[0] * other._c[0]) + (_c[1] * other._c[1]) + (_c[2] * other._c[2])
            + (2 * ((_c[3] * other._c[3]) + (_c[4] * other._c[4]) + (_c[5] * other._c[5])));

        return new Quantity(sum, Dimension * other.Dimension);
    }

    /// <summary>Сумма тензоров одной размерности</summary>
    public static SymmetricTensor operator +(SymmetricTensor a, SymmetricTensor b) => Combine(a, b, 1);

    /// <summary>Разность тензоров одной размерности</summary>
    public static SymmetricTensor operator -(SymmetricTensor a, SymmetricTensor b) => Combine(a, b, -1);

    /// <summary>Тензор с обратным знаком</summary>
    public static SymmetricTensor operator -(SymmetricTensor a) => Scale(a, -1);

    /// <summary>Умножение на число</summary>
    public static SymmetricTensor operator *(SymmetricTensor a, double k) => Scale(a, k);

    /// <summary>Умножение на число</summary>
    public static SymmetricTensor operator *(double k, SymmetricTensor a) => Scale(a, k);

    /// <summary>Шаровой тензор value·I заданной размерности</summary>
    internal static SymmetricTensor Isotropic(double value, Dimension dimension) => new(value, value, value, 0, 0, 0, dimension);

    /// <summary>Компоненты xx, yy, zz, xy, yz, zx в единицах СИ</summary>
    internal double[] Components => (double[])_c.Clone();

    /// <summary>Линейная комбинация a·x + b·I компонент в единицах СИ</summary>
    internal static SymmetricTensor Linear(SymmetricTensor x, double a, double b, Dimension dimension)
        => new((a * x._c[0]) + b, (a * x._c[1]) + b, (a * x._c[2]) + b, a * x._c[3], a * x._c[4], a * x._c[5], dimension);

    /// <inheritdoc />
    public bool Equals(SymmetricTensor? other)
        => other is not null && Dimension == other.Dimension && _c.SequenceEqual(other._c);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SymmetricTensor other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Dimension, _c[0], _c[1], _c[2], _c[3], _c[4], _c[5]);

    /// <summary>Матрица компонент в единицах СИ</summary>
    public override string ToString()
    {
        string F(double v) => v.ToString("G6", CultureInfo.InvariantCulture);

        return $"[{F(_c[0])} {F(_c[3])} {F(_c[5])}; {F(_c[3])} {F(_c[1])} {F(_c[4])}; {F(_c[5])} {F(_c[4])} {F(_c[2])}]"
            + (Dimension.IsDimensionless ? string.Empty : " " + Dimension);
    }

    private double J2Si
    {
        get
        {
            double a = _c[0] - _c[1], b = _c[1] - _c[2], c = _c[2] - _c[0];

            return (((a * a) + (b * b) + (c * c)) / 6) + (_c[3] * _c[3]) + (_c[4] * _c[4]) + (_c[5] * _c[5]);
        }
    }

    private Quantity Q(double value) => new(value, Dimension);

    private Vector3 Apply(Vector3 v)
        => new(
            (_c[0] * v.X) + (_c[3] * v.Y) + (_c[5] * v.Z),
            (_c[3] * v.X) + (_c[1] * v.Y) + (_c[4] * v.Z),
            (_c[5] * v.X) + (_c[4] * v.Y) + (_c[2] * v.Z));

    private static double Determinant(double[] c)
        => (c[0] * ((c[1] * c[2]) - (c[4] * c[4])))
            - (c[3] * ((c[3] * c[2]) - (c[4] * c[5])))
            + (c[5] * ((c[3] * c[4]) - (c[1] * c[5])));

    private static SymmetricTensor Combine(SymmetricTensor a, SymmetricTensor b, double sign)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Dimension != b.Dimension)
            throw new DimensionMismatchException(a.Dimension, b.Dimension, nameof(b));

        return new SymmetricTensor(
            a._c[0] + (sign * b._c[0]), a._c[1] + (sign * b._c[1]), a._c[2] + (sign * b._c[2]),
            a._c[3] + (sign * b._c[3]), a._c[4] + (sign * b._c[4]), a._c[5] + (sign * b._c[5]), a.Dimension);
    }

    private static SymmetricTensor Scale(SymmetricTensor a, double k)
    {
        ArgumentNullException.ThrowIfNull(a);

        return new SymmetricTensor(a._c[0] * k, a._c[1] * k, a._c[2] * k, a._c[3] * k, a._c[4] * k, a._c[5] * k, a.Dimension);
    }

    private static double StressComponent(Quantity value, string name)
        => value.Dimension.IsDimensionless && value.SiValue == 0.0 ? 0.0 : value.RequireSi(Dimension.Pressure, name);
}
