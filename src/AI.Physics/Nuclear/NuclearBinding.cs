using AI.Insights;
using AI.Physics.Internal;
using AI.Units;

namespace AI.Physics.Nuclear;

/// <summary>Энергия связи ядра по измеренной массе атома</summary>
/// <param name="Nuclide">Нуклид</param>
/// <param name="Energy">Энергия связи — работа, нужная, чтобы разобрать ядро на нуклоны</param>
/// <param name="MassDefect">Дефект масс: на столько ядро легче своих нуклонов по отдельности</param>
/// <param name="Predicted">Энергия связи по формуле Вайцзеккера</param>
public readonly record struct NuclearBinding(
    Nuclide Nuclide,
    Quantity Energy,
    Quantity MassDefect,
    Quantity? Predicted) : IInterpretable
{
    private static readonly SemiEmpiricalMassFormula DefaultModel = new();

    /// <summary>
    /// Масса атома водорода-1 из констант CODATA: протон плюс электрон минус энергия ионизации
    /// </summary>
    /// <remarks>
    /// Энергия ионизации — постоянная Ридберга с поправкой на приведённую массу, 13.598 эВ.
    /// Результат совпадает с таблицей масс AME до 10⁻⁹ а. е. м., поэтому для энергии связи
    /// не нужно ничего, кроме массы самого атома.
    /// </remarks>
    public static Quantity HydrogenAtomMass { get; } = ComputeHydrogenAtomMass();

    /// <summary>Энергия связи на нуклон</summary>
    public Quantity PerNucleon => new(Energy.SiValue / Nuclide.MassNumber, Dimension.Energy);

    /// <summary>Относительное отклонение формулы Вайцзеккера от измеренного значения</summary>
    public double? PredictionError => Predicted is { } predicted
        ? (predicted.SiValue - Energy.SiValue) / Energy.SiValue
        : null;

    /// <summary>
    /// Энергия связи по массе атома: <c>B = [Z·M(¹H) + N·m_n − M(A, Z)]c²</c>
    /// </summary>
    /// <remarks>
    /// Берутся массы атомов, как в таблицах масс: электроны сокращаются с электронами атомов
    /// водорода. Не учтена лишь разница в энергии связи самих электронов — меньше 0.05 % энергии
    /// связи ядра даже у свинца.
    /// </remarks>
    /// <param name="nuclide">Нуклид</param>
    /// <param name="atomicMass">Масса атома, например из AME2020</param>
    /// <param name="model">Формула для сравнения; по умолчанию Вайцзеккер с коэффициентами Рольфа</param>
    public static NuclearBinding FromAtomicMass(Nuclide nuclide, Quantity atomicMass, SemiEmpiricalMassFormula? model = null)
    {
        if (nuclide.MassNumber < 1)
            throw new ArgumentException("Нуклид не задан: массовое число должно быть положительным", nameof(nuclide));

        double mass = atomicMass.RequireSi(Dimension.MassDim, nameof(atomicMass));
        double dalton = Si.Dalton.Factor;

        // Дефект масс любого ядра — десятые доли атомной единицы: большее расхождение значит
        // перепутанный нуклид или единицы
        if (!(mass > 0) || Math.Abs((mass / dalton) - nuclide.MassNumber) > 0.2)
            throw new ArgumentException(
                $"Масса атома {mass / dalton:F4} а. е. м. не соответствует массовому числу {nuclide.MassNumber}",
                nameof(atomicMass));

        double defect = (nuclide.ProtonNumber * HydrogenAtomMass.SiValue)
            + (nuclide.NeutronNumber * PhysicalConstants.NeutronMass.SiValue)
            - mass;

        double c = PhysicalConstants.SpeedOfLight.SiValue;

        return new NuclearBinding(
            nuclide,
            new Quantity(defect * c * c, Dimension.Energy),
            new Quantity(defect, Dimension.MassDim),
            (model ?? DefaultModel).BindingEnergy(nuclide));
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int a = Nuclide.MassNumber;
        double perNucleon = PhysicsFormat.ToMeV(PerNucleon.SiValue);
        double defect = MassDefect.SiValue / Si.Dalton.Factor;
        double? error = PredictionError;
        bool strongerThanDrop = error is < 0;

        var builder = new InterpretationBuilder("Энергия связи ядра")
            .Summary($"Энергия связи {Nuclide}: {PhysicsFormat.Energy(Energy.SiValue)}, "
                + $"на нуклон {Fmt.Num(perNucleon, 3)} МэВ. Ядро легче своих нуклонов на {Fmt.Num(defect, 6)} а. е. м.")
            .Metric("Энергия связи", PhysicsFormat.Energy(Energy.SiValue))
            .Metric("На нуклон", perNucleon, "МэВ", "наибольшая — около 8.8 МэВ, у железа и никеля",
                perNucleon > 8.5 ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Дефект масс", defect, "а. е. м.", "разность масс нуклонов и ядра", MetricQuality.Unknown, 6);

        if (Predicted is { } predicted)
        {
            builder = builder
                .Metric("Формула Вайцзеккера", PhysicsFormat.Energy(predicted.SiValue), null, "модель заряженной капли")
                .Metric("Отклонение формулы", Fmt.Pct(error ?? 0, 2), null, "от измеренного",
                    Math.Abs(error ?? 0) <= 0.01 ? MetricQuality.Good : MetricQuality.Warning);
        }

        return builder
            .FindingIf(a > 1 && a < 56,
                "Ядро легче железа: связь на нуклон ниже наибольшей, и синтез из более лёгких ядер выделяет энергию — "
                + "так светят звёзды.")
            .FindingIf(a >= 56 && a <= 62,
                "Ядро у вершины кривой энергии связи: ни синтез, ни деление энергии не дают. Здесь кончается горение звёзд.")
            .FindingIf(a > 62,
                "Ядро тяжелее никеля: связь на нуклон убывает к тяжёлым ядрам из-за отталкивания протонов, поэтому деление "
                + "на осколки средней массы выделяет энергию.")
            .FindingIf(Nuclide.IsMagic && strongerThanDrop,
                "Число протонов или нейтронов магическое, и ядро связано сильнее, чем предсказывает капельная модель. Это "
                + "оболочечный эффект: заполненная оболочка, как у благородного газа, — формула Вайцзеккера о нём не знает.")
            .WarningIf(a < 20 && Predicted is not null,
                "Для лёгких ядер капельная модель ненадёжна: у них почти все нуклоны на поверхности, и формула ошибается "
                + "на десятки процентов.")
            .Warning("Использованы массы атомов: разница в энергии связи электронов не вычтена. Это меньше 0.05 % энергии "
                + "связи ядра даже у свинца.")
            .Build();
    }

    private static Quantity ComputeHydrogenAtomMass()
    {
        double proton = PhysicalConstants.ProtonMass.SiValue;
        double electron = PhysicalConstants.ElectronMass.SiValue;
        double c = PhysicalConstants.SpeedOfLight.SiValue;
        double h = PhysicalConstants.PlanckConstant.SiValue;
        double rydberg = PhysicalConstants.RydbergConstant.SiValue;

        // Энергия ионизации водорода: hcR∞ с поправкой на приведённую массу
        double ionisation = h * c * rydberg * proton / (proton + electron);

        return new Quantity(proton + electron - (ionisation / (c * c)), Dimension.MassDim);
    }
}
