using AI.Insights;
using AI.Units;

namespace AI.Physics.Continuum;

/// <summary>Оценка напряжённого состояния относительно предела текучести</summary>
public sealed class StressAssessment : IInterpretable
{
    internal StressAssessment(SymmetricTensor stress, Quantity yieldStrength)
    {
        Stress = stress;
        YieldStrength = yieldStrength;
        Principal = stress.Principal();
        VonMises = stress.VonMises;
        Tresca = Principal.First - Principal.Third;

        double yield = yieldStrength.SiValue;
        VonMisesSafetyFactor = VonMises.SiValue > 0 ? yield / VonMises.SiValue : double.PositiveInfinity;
        TrescaSafetyFactor = Tresca.SiValue > 0 ? yield / Tresca.SiValue : double.PositiveInfinity;
        Triaxiality = VonMises.SiValue > 0 ? stress.Mean.SiValue / VonMises.SiValue : double.NaN;
    }

    /// <summary>Тензор напряжений</summary>
    public SymmetricTensor Stress { get; }

    /// <summary>Предел текучести при одноосном растяжении</summary>
    public Quantity YieldStrength { get; }

    /// <summary>Главные напряжения</summary>
    public PrincipalValues Principal { get; }

    /// <summary>Эквивалентное напряжение по Мизесу</summary>
    public Quantity VonMises { get; }

    /// <summary>Эквивалентное напряжение по Треска σ₁ − σ₃</summary>
    public Quantity Tresca { get; }

    /// <summary>Запас по Мизесу: во сколько раз можно пропорционально увеличить нагрузку до текучести</summary>
    public double VonMisesSafetyFactor { get; }

    /// <summary>Запас по Треска — всегда не больше запаса по Мизесу, разница до 15,5 %</summary>
    public double TrescaSafetyFactor { get; }

    /// <summary>Трёхосность σ_m/σ_экв: чем она выше, тем меньше пластичность до разрушения</summary>
    public double Triaxiality { get; }

    /// <summary>Текучесть уже началась по критерию Мизеса</summary>
    public bool Yields => VonMisesSafetyFactor < 1;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double s1 = Principal.First.In("MPa"), s2 = Principal.Second.In("MPa"), s3 = Principal.Third.In("MPa");
        bool hydrostatic = VonMises.SiValue <= 1e-12 * Math.Max(1, Math.Abs(Stress.Mean.SiValue));

        return new InterpretationBuilder("Напряжённое состояние")
            .Summary(hydrostatic
                ? $"Состояние шаровое: все главные напряжения равны {Fmt.Num(s1, 3)} МПа. Касательных напряжений нет, "
                  + "и по критериям Мизеса и Треска текучесть не наступит при любом давлении."
                : $"Главные напряжения {Fmt.Num(s1, 3)}, {Fmt.Num(s2, 3)} и {Fmt.Num(s3, 3)} МПа. "
                  + $"Эквивалентное по Мизесу {Fmt.Num(VonMises.In("MPa"), 3)} МПа при пределе текучести "
                  + $"{Fmt.Num(YieldStrength.In("MPa"), 3)} МПа: запас {Fmt.Num(VonMisesSafetyFactor, 3)}"
                  + (Yields ? " — текучесть уже началась." : "."))
            .Metric("σ₁", Fmt.Num(s1, 3), "МПа", "наибольшее главное")
            .Metric("σ₂", Fmt.Num(s2, 3), "МПа", null)
            .Metric("σ₃", Fmt.Num(s3, 3), "МПа", "наименьшее главное")
            .Metric("По Мизесу", Fmt.Num(VonMises.In("MPa"), 3), "МПа", "энергия формоизменения")
            .Metric("По Треска", Fmt.Num(Tresca.In("MPa"), 3), "МПа", "удвоенное наибольшее касательное")
            .Metric("Запас по Мизесу", Fmt.Num(VonMisesSafetyFactor, 3), null, null,
                Yields ? MetricQuality.Critical : VonMisesSafetyFactor < 1.5 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Запас по Треска", Fmt.Num(TrescaSafetyFactor, 3), null, "осторожнее Мизеса")
            .Metric("Трёхосность", Fmt.Num(Triaxiality, 3), null, "среднее напряжение, делённое на эквивалентное")
            .FindingIf(!hydrostatic && Math.Abs(Stress.Mean.SiValue) > VonMises.SiValue,
                "Шаровая часть больше эквивалентного напряжения. Металл она не приводит к текучести — течёт он от "
                + "формоизменения, — но растягивающая шаровая часть открывает поры и трещины.")
            .FindingIf(!hydrostatic && TrescaSafetyFactor < VonMisesSafetyFactor * 0.995,
                $"Треска даёт запас на {Fmt.Pct(1 - (TrescaSafetyFactor / VonMisesSafetyFactor))} меньше Мизеса. "
                + "Эксперимент для пластичных металлов ближе к Мизесу; Треска проще и никогда не завышает запас.")
            .WarningIf(Triaxiality > 1,
                "Трёхосность выше единицы: так бывает у надрезов и в толстых сечениях. Пластичный материал здесь "
                + "может разрушиться хрупко, почти без пластической деформации, и запас по текучести этого не покажет.")
            .Warning("Критерии текучести описывают начало пластичности изотропного металла. Хрупкие материалы — "
                + "чугун, керамика, бетон, грунт — разрушаются по другим законам: для них нужен Мора — Кулона или механика трещин.")
            .Build();
    }
}

/// <summary>
/// Критерии перехода в пластическое состояние и разрушения.
/// </summary>
/// <remarks>
/// <para>
/// Мизес: текучесть начинается, когда энергия формоизменения достигает значения при одноосном
/// пределе текучести, <c>√(3·J₂) = σ_T</c>. Треска: когда наибольшее касательное достигает σ_T/2.
/// В пространстве главных напряжений это цилиндр и вписанная в него шестигранная призма — отсюда
/// расхождение не больше 2/√3, то есть 15,5 %, и именно при чистом сдвиге.
/// </para>
/// <para>
/// Мора — Кулона — для грунтов, пород и бетона: сопротивление сдвигу растёт со сжатием,
/// <c>τ = c + σ·tg φ</c> на площадке разрушения, где c — сцепление, φ — угол внутреннего трения.
/// </para>
/// </remarks>
public static class YieldCriteria
{
    /// <summary>Оценивает напряжённое состояние по критериям Мизеса и Треска</summary>
    /// <param name="stress">Тензор напряжений</param>
    /// <param name="yieldStrength">Предел текучести при одноосном растяжении</param>
    public static StressAssessment Assess(SymmetricTensor stress, Quantity yieldStrength)
    {
        ArgumentNullException.ThrowIfNull(stress);

        if (stress.Dimension != Dimension.Pressure)
            throw new DimensionMismatchException(Dimension.Pressure, stress.Dimension, nameof(stress));

        double yield = yieldStrength.RequireSi(Dimension.Pressure, nameof(yieldStrength));

        if (!(yield > 0))
            throw new ArgumentOutOfRangeException(nameof(yieldStrength), "Предел текучести должен быть положительным");

        return new StressAssessment(stress, yieldStrength);
    }

    /// <summary>
    /// Запас по Мору — Кулону при пропорциональном росте нагрузки
    /// </summary>
    /// <param name="stress">Тензор напряжений; сжатие отрицательно</param>
    /// <param name="cohesion">Сцепление c</param>
    /// <param name="frictionAngleDegrees">Угол внутреннего трения φ, градусы</param>
    /// <returns>Во сколько раз можно увеличить все напряжения до разрушения; бесконечность — не разрушится</returns>
    public static double MohrCoulombSafetyFactor(SymmetricTensor stress, Quantity cohesion, double frictionAngleDegrees)
    {
        ArgumentNullException.ThrowIfNull(stress);

        double c = cohesion.RequireSi(Dimension.Pressure, nameof(cohesion));

        if (c < 0 || !(frictionAngleDegrees >= 0 && frictionAngleDegrees < 90))
            throw new ArgumentOutOfRangeException(nameof(frictionAngleDegrees), "Нужны c ≥ 0 и 0 ≤ φ < 90°");

        PrincipalValues p = stress.Principal();
        double phi = frictionAngleDegrees * Math.PI / 180;
        double radius = (p.First.SiValue - p.Third.SiValue) / 2;
        double center = (p.First.SiValue + p.Third.SiValue) / 2;

        // Круг Мора касается прямой τ = c − σ·tg φ (растяжение положительно), когда
        // R + C·sin φ = c·cos φ; при пропорциональном масштабе n разрушение при n(R + C·sin φ) = c·cos φ
        double demand = radius + (center * Math.Sin(phi));

        return demand <= 0 ? double.PositiveInfinity : c * Math.Cos(phi) / demand;
    }
}
