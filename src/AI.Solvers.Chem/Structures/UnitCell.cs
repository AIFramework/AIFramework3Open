using AI.Geometry.Primitives;
using AI.Units;
using System.Globalization;

namespace AI.Solvers.Chem.Structures;

/// <summary>
/// Сингония кристалла
/// </summary>
public enum CrystalSystem
{
    /// <summary>Триклинная</summary>
    Triclinic,

    /// <summary>Моноклинная</summary>
    Monoclinic,

    /// <summary>Ромбическая</summary>
    Orthorhombic,

    /// <summary>Тетрагональная</summary>
    Tetragonal,

    /// <summary>Тригональная (ромбоэдрическая)</summary>
    Trigonal,

    /// <summary>Гексагональная</summary>
    Hexagonal,

    /// <summary>Кубическая</summary>
    Cubic
}

/// <summary>
/// Элементарная ячейка: параметры решётки и переход между дробными
/// и декартовыми координатами
/// </summary>
/// <remarks>
/// Используется стандартная установка: вектор a направлен по оси X, вектор b лежит
/// в плоскости XY. Межплоскостные расстояния считаются через векторы обратной решётки,
/// поэтому формула одна для всех сингоний, без разбора частных случаев.
/// </remarks>
public sealed class UnitCell
{
    /// <summary>Параметр a как физическая величина</summary>
    public Quantity LengthA => Quantity.Of(A, Si.Angstrom);

    /// <summary>Параметр b как физическая величина</summary>
    public Quantity LengthB => Quantity.Of(B, Si.Angstrom);

    /// <summary>Параметр c как физическая величина</summary>
    public Quantity LengthC => Quantity.Of(C, Si.Angstrom);

    /// <summary>Объём ячейки как физическая величина</summary>
    public Quantity CellVolume => Quantity.Of(Volume, Si.Angstrom.Pow(3));

        /// <summary>Параметр a, ангстремы</summary>
    public double A { get; }

    /// <summary>Параметр b, ангстремы</summary>
    public double B { get; }

    /// <summary>Параметр c, ангстремы</summary>
    public double C { get; }

    /// <summary>Угол alpha между b и c, градусы</summary>
    public double Alpha { get; }

    /// <summary>Угол beta между a и c, градусы</summary>
    public double Beta { get; }

    /// <summary>Угол gamma между a и b, градусы</summary>
    public double Gamma { get; }

    /// <summary>Вектор a</summary>
    public Vector3 VectorA { get; }

    /// <summary>Вектор b</summary>
    public Vector3 VectorB { get; }

    /// <summary>Вектор c</summary>
    public Vector3 VectorC { get; }

    /// <summary>Объём ячейки, кубические ангстремы</summary>
    public double Volume { get; }

    /// <summary>Создаёт ячейку по параметрам решётки</summary>
    /// <param name="a">Параметр a, ангстремы</param>
    /// <param name="b">Параметр b, ангстремы</param>
    /// <param name="c">Параметр c, ангстремы</param>
    /// <param name="alpha">Угол alpha, градусы</param>
    /// <param name="beta">Угол beta, градусы</param>
    /// <param name="gamma">Угол gamma, градусы</param>
    public UnitCell(double a, double b, double c, double alpha = 90, double beta = 90, double gamma = 90)
    {
        if (a <= 0 || b <= 0 || c <= 0)
            throw new ArgumentException("Параметры ячейки должны быть положительными");

        if (alpha is <= 0 or >= 180 || beta is <= 0 or >= 180 || gamma is <= 0 or >= 180)
            throw new ArgumentException("Углы ячейки должны лежать в интервале (0; 180) градусов");

        A = a;
        B = b;
        C = c;
        Alpha = alpha;
        Beta = beta;
        Gamma = gamma;

        double cosAlpha = Math.Cos(alpha * Math.PI / 180);
        double cosBeta = Math.Cos(beta * Math.PI / 180);
        double cosGamma = Math.Cos(gamma * Math.PI / 180);
        double sinGamma = Math.Sin(gamma * Math.PI / 180);

        double factor = 1 - (cosAlpha * cosAlpha) - (cosBeta * cosBeta) - (cosGamma * cosGamma)
            + (2 * cosAlpha * cosBeta * cosGamma);

        if (factor <= 0)
            throw new ArgumentException("Заданные углы не образуют возможную ячейку");

        Volume = a * b * c * Math.Sqrt(factor);

        VectorA = new Vector3(a, 0, 0);
        VectorB = new Vector3(b * cosGamma, b * sinGamma, 0);
        VectorC = new Vector3(
            c * cosBeta,
            c * (cosAlpha - (cosBeta * cosGamma)) / sinGamma,
            Volume / (a * b * sinGamma));
    }

    /// <summary>Кубическая ячейка</summary>
    /// <param name="a">Ребро, ангстремы</param>
    public static UnitCell Cubic(double a) => new(a, a, a);

    /// <summary>Гексагональная ячейка</summary>
    /// <param name="a">Параметр a</param>
    /// <param name="c">Параметр c</param>
    public static UnitCell Hexagonal(double a, double c) => new(a, a, c, 90, 90, 120);

    /// <summary>Тетрагональная ячейка</summary>
    /// <param name="a">Параметр a</param>
    /// <param name="c">Параметр c</param>
    public static UnitCell Tetragonal(double a, double c) => new(a, a, c);

    /// <summary>Сингония, определённая по параметрам решётки</summary>
    public CrystalSystem System
    {
        get
        {
            bool ab = Close(A, B), bc = Close(B, C);
            bool alpha90 = Close(Alpha, 90), beta90 = Close(Beta, 90), gamma90 = Close(Gamma, 90);

            if (ab && bc && alpha90 && beta90 && gamma90)
                return CrystalSystem.Cubic;

            if (ab && alpha90 && beta90 && Close(Gamma, 120))
                return CrystalSystem.Hexagonal;

            if (ab && bc && Close(Alpha, Beta) && Close(Beta, Gamma) && !alpha90)
                return CrystalSystem.Trigonal;

            if (ab && alpha90 && beta90 && gamma90)
                return CrystalSystem.Tetragonal;

            if (alpha90 && beta90 && gamma90)
                return CrystalSystem.Orthorhombic;

            if (alpha90 && gamma90)
                return CrystalSystem.Monoclinic;

            return CrystalSystem.Triclinic;
        }
    }

    /// <summary>Декартовы координаты по дробным</summary>
    /// <param name="fractional">Дробные координаты</param>
    public Vector3 ToCartesian(Vector3 fractional)
        => (VectorA * fractional.X) + (VectorB * fractional.Y) + (VectorC * fractional.Z);

    /// <summary>Дробные координаты по декартовым</summary>
    /// <param name="cartesian">Декартовы координаты</param>
    public Vector3 ToFractional(Vector3 cartesian)
    {
        // Решение системы через обратные векторы решётки: f = r · a*, где a* = (b x c)/V
        Vector3 starA = VectorB.Cross(VectorC) / Volume;
        Vector3 starB = VectorC.Cross(VectorA) / Volume;
        Vector3 starC = VectorA.Cross(VectorB) / Volume;

        return new Vector3(cartesian.Dot(starA), cartesian.Dot(starB), cartesian.Dot(starC));
    }

    /// <summary>Вектор обратной решётки для отражения hkl (в единицах 1/ангстрем)</summary>
    /// <param name="h">Индекс h</param>
    /// <param name="k">Индекс k</param>
    /// <param name="l">Индекс l</param>
    public Vector3 ReciprocalVector(int h, int k, int l)
    {
        Vector3 starA = VectorB.Cross(VectorC) / Volume;
        Vector3 starB = VectorC.Cross(VectorA) / Volume;
        Vector3 starC = VectorA.Cross(VectorB) / Volume;

        return (starA * h) + (starB * k) + (starC * l);
    }

    /// <summary>Межплоскостное расстояние для отражения hkl, ангстремы</summary>
    /// <param name="h">Индекс h</param>
    /// <param name="k">Индекс k</param>
    /// <param name="l">Индекс l</param>
    public double InterplanarSpacing(int h, int k, int l)
    {
        if (h == 0 && k == 0 && l == 0)
            return double.PositiveInfinity;

        return 1.0 / ReciprocalVector(h, k, l).Length;
    }

    /// <summary>
    /// Угол Брэгга для отражения, градусы; NaN, если отражение недостижимо
    /// при данной длине волны
    /// </summary>
    /// <param name="h">Индекс h</param>
    /// <param name="k">Индекс k</param>
    /// <param name="l">Индекс l</param>
    /// <param name="wavelength">Длина волны, ангстремы</param>
    public double BraggAngle(int h, int k, int l, double wavelength)
    {
        double d = InterplanarSpacing(h, k, l);
        double sine = wavelength / (2 * d);

        return sine is > 1 or <= 0 ? double.NaN : Math.Asin(sine) * 180 / Math.PI;
    }

    /// <summary>
    /// Рентгеновская плотность, г/см3
    /// </summary>
    /// <param name="formulaMass">Молярная масса формульной единицы, г/моль</param>
    /// <param name="formulaUnits">Число формульных единиц в ячейке (Z)</param>
    public double Density(double formulaMass, int formulaUnits)
        => formulaUnits * formulaMass / (0.602214076 * Volume);

    /// <summary>Кратчайший вектор между точками с учётом периодичности</summary>
    /// <param name="from">Начало, декартовы координаты</param>
    /// <param name="to">Конец, декартовы координаты</param>
    public Vector3 MinimumImage(Vector3 from, Vector3 to)
    {
        Vector3 fractional = ToFractional(to - from);

        return ToCartesian(new Vector3(
            fractional.X - Math.Round(fractional.X),
            fractional.Y - Math.Round(fractional.Y),
            fractional.Z - Math.Round(fractional.Z)));
    }

    /// <summary>Параметры ячейки строкой</summary>
    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture,
            "a = {0:F4}, b = {1:F4}, c = {2:F4}, alpha = {3:F2}, beta = {4:F2}, gamma = {5:F2}, V = {6:F2} A^3",
            A, B, C, Alpha, Beta, Gamma, Volume);

    private static bool Close(double left, double right) => Math.Abs(left - right) < 1e-4;
}
