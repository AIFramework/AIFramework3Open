using AI.Insights;
using AI.Units;

namespace AI.Physics.Continuum;

/// <summary>Оценка элемента с трещиной</summary>
public sealed class CrackAssessment : IInterpretable
{
    internal CrackAssessment(
        double stress, double crackLength, double toughness, double yieldStrength, double geometryFactor)
    {
        double k = geometryFactor * stress * Math.Sqrt(Math.PI * crackLength);

        StressIntensity = new Quantity(k, FractureMechanics.StressIntensityDimension);
        Toughness = new Quantity(toughness, FractureMechanics.StressIntensityDimension);
        SafetyFactor = k > 0 ? toughness / k : double.PositiveInfinity;
        CriticalCrackLength = new Quantity(Math.Pow(toughness / (geometryFactor * stress), 2) / Math.PI, Dimension.LengthDim);
        CriticalStress = new Quantity(toughness / (geometryFactor * Math.Sqrt(Math.PI * crackLength)), Dimension.Pressure);
        PlasticZoneSize = new Quantity(Math.Pow(k / yieldStrength, 2) / (2 * Math.PI), Dimension.LengthDim);
        NetSectionYielding = stress >= yieldStrength;
        CrackLength = new Quantity(crackLength, Dimension.LengthDim);
    }

    /// <summary>Коэффициент интенсивности напряжений K_I = Yσ√(πa)</summary>
    public Quantity StressIntensity { get; }

    /// <summary>Вязкость разрушения K_IC</summary>
    public Quantity Toughness { get; }

    /// <summary>Запас K_IC/K_I</summary>
    public double SafetyFactor { get; }

    /// <summary>Длина трещины</summary>
    public Quantity CrackLength { get; }

    /// <summary>Критическая длина трещины при данном напряжении</summary>
    public Quantity CriticalCrackLength { get; }

    /// <summary>Критическое напряжение при данной трещине</summary>
    public Quantity CriticalStress { get; }

    /// <summary>Размер пластической зоны у вершины по Ирвину (плоское напряжённое состояние)</summary>
    public Quantity PlasticZoneSize { get; }

    /// <summary>Номинальное напряжение уже выше предела текучести</summary>
    public bool NetSectionYielding { get; }

    /// <summary>Разрушение ожидается: K_I ≥ K_IC</summary>
    public bool Fractures => SafetyFactor <= 1;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double zoneRatio = PlasticZoneSize.SiValue / CrackLength.SiValue;

        return new InterpretationBuilder("Механика разрушения")
            .Summary($"K_I = {Fmt.Num(StressIntensity.SiValue / 1e6, 3)} МПа·√м при вязкости разрушения "
                + $"{Fmt.Num(Toughness.SiValue / 1e6, 3)}: запас {Fmt.Num(SafetyFactor, 3)}. "
                + $"Критическая длина трещины {Fmt.Num(CriticalCrackLength.In("mm"), 3)} мм, сейчас "
                + $"{Fmt.Num(CrackLength.In("mm"), 3)} мм" + (Fractures ? " — трещина растёт нестабильно." : "."))
            .Metric("K_I", Fmt.Num(StressIntensity.SiValue / 1e6, 3), "МПа·√м", "коэффициент интенсивности напряжений",
                Fractures ? MetricQuality.Critical : SafetyFactor < 2 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Запас", Fmt.Num(SafetyFactor, 3), null, "K_IC / K_I")
            .Metric("Критическая длина", Fmt.Num(CriticalCrackLength.In("mm"), 3), "мм", "при действующем напряжении")
            .Metric("Критическое напряжение", Fmt.Num(CriticalStress.In("MPa"), 3), "МПа", "при действующей трещине")
            .Metric("Пластическая зона", Fmt.Num(PlasticZoneSize.In("mm"), 3), "мм", "у вершины трещины, по Ирвину")
            .FindingIf(!Fractures,
                "Запас по трещине, а не по прочности: длина трещины растёт как квадрат отношения K_IC/(Yσ), "
                + "поэтому снижение напряжения вдвое вчетверо увеличивает допустимую трещину.")
            .WarningIf(NetSectionYielding,
                "Номинальное напряжение выше предела текучести: сечение течёт раньше, чем трещина разрушит его хрупко, "
                + "и линейная механика разрушения неприменима.")
            .WarningIf(zoneRatio > 0.02,
                $"Пластическая зона составляет {Fmt.Pct(zoneRatio)} длины трещины: при зоне больше нескольких процентов "
                + "условие малой текучести у вершины нарушено, и K_I недооценивает опасность — нужны J-интеграл или раскрытие трещины.")
            .Warning("Коэффициент формы Y берётся из справочника для конкретной геометрии: 1 — сквозная трещина "
                + "в бесконечной пластине, около 1,12 — краевая. Ошибка в Y переходит в K один к одному.")
            .Build();
    }
}

/// <summary>
/// Линейная механика разрушения: коэффициент интенсивности напряжений и критерий Ирвина.
/// </summary>
/// <remarks>
/// <para>
/// У вершины трещины напряжения в линейно-упругом теле бесконечны, как 1/√r, и прочность теряет
/// смысл. Ирвин (1957) заменил её коэффициентом интенсивности <c>K_I = Yσ√(πa)</c>: трещина растёт,
/// когда K_I достигает вязкости разрушения материала K_IC. Энергетический критерий Гриффита
/// <c>G = K²/E′</c> ему эквивалентен, где E′ = E при плоском напряжённом состоянии и E/(1 − ν²) при
/// плоской деформации.
/// </para>
/// <para>
/// Для полусквозной трещины длиной 2a в бесконечной пластине Y = 1, для краевой трещины длины a —
/// около 1,12. Приближение верно, пока пластическая зона у вершины мала по сравнению с трещиной.
/// </para>
/// </remarks>
public static class FractureMechanics
{
    /// <summary>Размерность K: Па·√м</summary>
    public static Dimension StressIntensityDimension { get; } = Dimension.Pressure * Dimension.LengthDim.Sqrt();

    /// <summary>Коэффициент интенсивности напряжений K_I = Yσ√(πa)</summary>
    /// <param name="stress">Номинальное растягивающее напряжение</param>
    /// <param name="crackLength">Полудлина сквозной трещины или длина краевой a</param>
    /// <param name="geometryFactor">Коэффициент формы Y</param>
    public static Quantity StressIntensity(Quantity stress, Quantity crackLength, double geometryFactor = 1.0)
    {
        (double s, double a) = Read(stress, crackLength, geometryFactor);

        return new Quantity(geometryFactor * s * Math.Sqrt(Math.PI * a), StressIntensityDimension);
    }

    /// <summary>Критическая длина трещины (1/π)·(K_IC/(Yσ))²</summary>
    /// <param name="stress">Номинальное напряжение</param>
    /// <param name="toughness">Вязкость разрушения K_IC</param>
    /// <param name="geometryFactor">Коэффициент формы Y</param>
    public static Quantity CriticalCrackLength(Quantity stress, Quantity toughness, double geometryFactor = 1.0)
    {
        double s = stress.RequireSi(Dimension.Pressure, nameof(stress));
        double k = Toughness(toughness);

        if (!(s > 0) || !(geometryFactor > 0))
            throw new ArgumentOutOfRangeException(nameof(stress), "Напряжение и коэффициент формы должны быть положительными");

        return new Quantity(Math.Pow(k / (geometryFactor * s), 2) / Math.PI, Dimension.LengthDim);
    }

    /// <summary>Интенсивность высвобождения энергии G = K²/E′</summary>
    /// <param name="stressIntensity">Коэффициент интенсивности K_I</param>
    /// <param name="material">Материал</param>
    /// <param name="planeStrain">Плоская деформация — толстое тело; иначе плоское напряжённое — тонкая пластина</param>
    public static Quantity EnergyReleaseRate(Quantity stressIntensity, ElasticMaterial material, bool planeStrain = true)
    {
        ArgumentNullException.ThrowIfNull(material);

        double k = stressIntensity.RequireSi(StressIntensityDimension, nameof(stressIntensity));
        double modulus = planeStrain ? material.PlaneStrainModulus.SiValue : material.YoungModulus.SiValue;

        return new Quantity(k * k / modulus, Dimension.Energy / Dimension.Area);
    }

    /// <summary>Оценка элемента с трещиной</summary>
    /// <param name="stress">Номинальное напряжение</param>
    /// <param name="crackLength">Длина трещины a</param>
    /// <param name="toughness">Вязкость разрушения K_IC</param>
    /// <param name="yieldStrength">Предел текучести</param>
    /// <param name="geometryFactor">Коэффициент формы Y</param>
    public static CrackAssessment Assess(
        Quantity stress, Quantity crackLength, Quantity toughness, Quantity yieldStrength, double geometryFactor = 1.0)
    {
        (double s, double a) = Read(stress, crackLength, geometryFactor);
        double yield = yieldStrength.RequireSi(Dimension.Pressure, nameof(yieldStrength));

        if (!(yield > 0))
            throw new ArgumentOutOfRangeException(nameof(yieldStrength), "Предел текучести должен быть положительным");

        return new CrackAssessment(s, a, Toughness(toughness), yield, geometryFactor);
    }

    private static (double Stress, double Length) Read(Quantity stress, Quantity crackLength, double geometryFactor)
    {
        double s = stress.RequireSi(Dimension.Pressure, nameof(stress));
        double a = crackLength.RequireSi(Dimension.LengthDim, nameof(crackLength));

        if (!(s > 0) || !(a > 0) || !(geometryFactor > 0))
            throw new ArgumentOutOfRangeException(nameof(stress), "Напряжение, длина трещины и коэффициент формы должны быть положительными");

        return (s, a);
    }

    private static double Toughness(Quantity toughness)
    {
        double k = toughness.RequireSi(StressIntensityDimension, nameof(toughness));

        return k > 0 ? k : throw new ArgumentOutOfRangeException(nameof(toughness), "Вязкость разрушения должна быть положительной");
    }
}
