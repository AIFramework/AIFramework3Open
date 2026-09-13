using AI.Units;

namespace AI.Physics.Continuum;

/// <summary>Закрепление концов сжатого стержня</summary>
public enum ColumnEnds
{
    /// <summary>Оба конца шарнирные: коэффициент приведения длины 1</summary>
    PinnedPinned,

    /// <summary>Один конец защемлён, другой свободен: коэффициент 2</summary>
    FixedFree,

    /// <summary>Один защемлён, другой шарнирный: коэффициент 0,699</summary>
    FixedPinned,

    /// <summary>Оба защемлены: коэффициент 0,5</summary>
    FixedFixed
}

/// <summary>
/// Изгиб балок по Эйлеру — Бернулли и устойчивость сжатых стержней.
/// </summary>
/// <remarks>
/// <para>
/// Балка тонкая: сечения остаются плоскими и перпендикулярными оси, сдвиговой податливости нет.
/// Для балок короче примерно десяти высот сдвиг заметен, и прогиб по этим формулам занижен
/// (поправка Тимошенко здесь не учтена).
/// </para>
/// <para>
/// Критическая сила Эйлера <c>π²EI/(KL)²</c> верна для гибких стержней. У коротких потеря устойчивости
/// наступает раньше — от текучести, и формула Эйлера завышает несущую способность; граница —
/// гибкость <c>KL/i</c> примерно π√(E/σ_T).
/// </para>
/// </remarks>
public static class Beams
{
    /// <summary>Момент инерции прямоугольного сечения относительно оси, параллельной ширине: bh³/12</summary>
    /// <param name="width">Ширина b</param>
    /// <param name="height">Высота h — в плоскости изгиба</param>
    public static Quantity RectangleSecondMoment(Quantity width, Quantity height)
    {
        double b = Length(width, nameof(width));
        double h = Length(height, nameof(height));

        return new Quantity(b * h * h * h / 12, SecondMomentDimension);
    }

    /// <summary>Момент инерции круглого сечения πd⁴/64; для трубы — разность наружного и внутреннего</summary>
    /// <param name="diameter">Наружный диаметр</param>
    /// <param name="innerDiameter">Внутренний диаметр; по умолчанию сплошное сечение</param>
    public static Quantity CircleSecondMoment(Quantity diameter, Quantity innerDiameter = default)
    {
        (double d, double inner) = Diameters(diameter, innerDiameter);

        return new Quantity(Math.PI * (Math.Pow(d, 4) - Math.Pow(inner, 4)) / 64, SecondMomentDimension);
    }

    /// <summary>Прогиб конца консоли от силы на конце: FL³/(3EI)</summary>
    /// <param name="force">Сила</param>
    /// <param name="length">Длина</param>
    /// <param name="material">Материал</param>
    /// <param name="secondMoment">Момент инерции сечения</param>
    public static Quantity CantileverTipDeflection(Quantity force, Quantity length, ElasticMaterial material, Quantity secondMoment)
        => Deflection(force.RequireSi(Dimension.Force, nameof(force)), length, material, secondMoment, 3, 1.0 / 3);

    /// <summary>Прогиб конца консоли от равномерной нагрузки: wL⁴/(8EI)</summary>
    /// <param name="loadPerLength">Нагрузка на единицу длины</param>
    /// <param name="length">Длина</param>
    /// <param name="material">Материал</param>
    /// <param name="secondMoment">Момент инерции сечения</param>
    public static Quantity CantileverUniformDeflection(Quantity loadPerLength, Quantity length, ElasticMaterial material, Quantity secondMoment)
        => Deflection(LineLoad(loadPerLength), length, material, secondMoment, 4, 1.0 / 8);

    /// <summary>Прогиб середины шарнирно опёртой балки от силы в середине: FL³/(48EI)</summary>
    /// <param name="force">Сила</param>
    /// <param name="length">Пролёт</param>
    /// <param name="material">Материал</param>
    /// <param name="secondMoment">Момент инерции сечения</param>
    public static Quantity SimplySupportedCentralDeflection(Quantity force, Quantity length, ElasticMaterial material, Quantity secondMoment)
        => Deflection(force.RequireSi(Dimension.Force, nameof(force)), length, material, secondMoment, 3, 1.0 / 48);

    /// <summary>Прогиб середины шарнирно опёртой балки от равномерной нагрузки: 5wL⁴/(384EI)</summary>
    /// <param name="loadPerLength">Нагрузка на единицу длины</param>
    /// <param name="length">Пролёт</param>
    /// <param name="material">Материал</param>
    /// <param name="secondMoment">Момент инерции сечения</param>
    public static Quantity SimplySupportedUniformDeflection(Quantity loadPerLength, Quantity length, ElasticMaterial material, Quantity secondMoment)
        => Deflection(LineLoad(loadPerLength), length, material, secondMoment, 4, 5.0 / 384);

    /// <summary>Нормальное напряжение изгиба My/I</summary>
    /// <param name="bendingMoment">Изгибающий момент</param>
    /// <param name="distanceFromNeutralAxis">Расстояние от нейтральной оси</param>
    /// <param name="secondMoment">Момент инерции сечения</param>
    public static Quantity BendingStress(Quantity bendingMoment, Quantity distanceFromNeutralAxis, Quantity secondMoment)
    {
        double m = bendingMoment.RequireSi(Dimension.Energy, nameof(bendingMoment));
        double y = distanceFromNeutralAxis.RequireSi(Dimension.LengthDim, nameof(distanceFromNeutralAxis));
        double i = SecondMoment(secondMoment);

        return new Quantity(m * y / i, Dimension.Pressure);
    }

    /// <summary>Критическая сила Эйлера π²EI/(KL)²</summary>
    /// <param name="material">Материал</param>
    /// <param name="secondMoment">Наименьший момент инерции сечения</param>
    /// <param name="length">Длина стержня</param>
    /// <param name="ends">Закрепление концов</param>
    public static Quantity EulerBucklingLoad(ElasticMaterial material, Quantity secondMoment, Quantity length, ColumnEnds ends)
    {
        ArgumentNullException.ThrowIfNull(material);

        double effective = EffectiveLengthFactor(ends) * Length(length, nameof(length));

        return new Quantity(Math.PI * Math.PI * material.YoungModulus.SiValue * SecondMoment(secondMoment) / (effective * effective), Dimension.Force);
    }

    /// <summary>Коэффициент приведения длины K</summary>
    /// <param name="ends">Закрепление концов</param>
    public static double EffectiveLengthFactor(ColumnEnds ends) => ends switch
    {
        ColumnEnds.PinnedPinned => 1.0,
        ColumnEnds.FixedFree => 2.0,
        // Корень уравнения tg(kL) = kL: kL = 4,4934, K = π/4,4934
        ColumnEnds.FixedPinned => Math.PI / 4.493409457909064,
        _ => 0.5
    };

    /// <summary>Размерность момента инерции сечения, м⁴</summary>
    public static Dimension SecondMomentDimension { get; } = Dimension.LengthDim.Pow(4);

    private static Quantity Deflection(double load, Quantity length, ElasticMaterial material, Quantity secondMoment, int power, double factor)
    {
        ArgumentNullException.ThrowIfNull(material);

        double l = Length(length, nameof(length));

        return new Quantity(factor * load * Math.Pow(l, power) / (material.YoungModulus.SiValue * SecondMoment(secondMoment)), Dimension.LengthDim);
    }

    private static double LineLoad(Quantity loadPerLength)
        => loadPerLength.RequireSi(Dimension.Force / Dimension.LengthDim, nameof(loadPerLength));

    private static double SecondMoment(Quantity value)
    {
        double i = value.RequireSi(SecondMomentDimension, nameof(value));

        return i > 0 ? i : throw new ArgumentOutOfRangeException(nameof(value), "Момент инерции должен быть положительным");
    }

    internal static double Length(Quantity value, string name)
    {
        double l = value.RequireSi(Dimension.LengthDim, name);

        return l > 0 ? l : throw new ArgumentOutOfRangeException(name, "Длина должна быть положительной");
    }

    internal static (double Outer, double Inner) Diameters(Quantity diameter, Quantity innerDiameter)
    {
        double d = Length(diameter, nameof(diameter));
        double inner = innerDiameter.Dimension.IsDimensionless && innerDiameter.SiValue == 0
            ? 0
            : innerDiameter.RequireSi(Dimension.LengthDim, nameof(innerDiameter));

        return inner >= 0 && inner < d
            ? (d, inner)
            : throw new ArgumentOutOfRangeException(nameof(innerDiameter), "Внутренний диаметр должен быть меньше наружного");
    }
}

/// <summary>
/// Кручение вала круглого сечения.
/// </summary>
/// <remarks>
/// Касательное напряжение растёт от оси к поверхности линейно, <c>τ = T·r/J</c>, где <c>J = πd⁴/32</c>.
/// Поэтому полый вал той же массы прочнее: сердцевина сплошного почти не нагружена. Для некруглых
/// сечений сечения депланируют, и эти формулы неприменимы.
/// </remarks>
public static class Torsion
{
    /// <summary>Полярный момент инерции круглого сечения πd⁴/32</summary>
    /// <param name="diameter">Наружный диаметр</param>
    /// <param name="innerDiameter">Внутренний; по умолчанию сплошной вал</param>
    public static Quantity PolarMoment(Quantity diameter, Quantity innerDiameter = default)
    {
        (double d, double inner) = Beams.Diameters(diameter, innerDiameter);

        return new Quantity(Math.PI * (Math.Pow(d, 4) - Math.Pow(inner, 4)) / 32, Beams.SecondMomentDimension);
    }

    /// <summary>Наибольшее касательное напряжение на поверхности вала</summary>
    /// <param name="torque">Крутящий момент</param>
    /// <param name="diameter">Наружный диаметр</param>
    /// <param name="innerDiameter">Внутренний диаметр</param>
    public static Quantity ShearStress(Quantity torque, Quantity diameter, Quantity innerDiameter = default)
    {
        double t = torque.RequireSi(Dimension.Energy, nameof(torque));
        double d = Beams.Length(diameter, nameof(diameter));

        return new Quantity(t * (d / 2) / PolarMoment(diameter, innerDiameter).SiValue, Dimension.Pressure);
    }

    /// <summary>Угол закручивания TL/(GJ), радианы</summary>
    /// <param name="torque">Крутящий момент</param>
    /// <param name="length">Длина вала</param>
    /// <param name="material">Материал</param>
    /// <param name="diameter">Наружный диаметр</param>
    /// <param name="innerDiameter">Внутренний диаметр</param>
    public static double TwistAngle(Quantity torque, Quantity length, ElasticMaterial material, Quantity diameter, Quantity innerDiameter = default)
    {
        ArgumentNullException.ThrowIfNull(material);

        double t = torque.RequireSi(Dimension.Energy, nameof(torque));
        double l = Beams.Length(length, nameof(length));

        return t * l / (material.ShearModulus.SiValue * PolarMoment(diameter, innerDiameter).SiValue);
    }
}
