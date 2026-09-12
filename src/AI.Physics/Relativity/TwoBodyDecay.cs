using AI.Insights;
using AI.Physics.Internal;
using AI.Units;

namespace AI.Physics.Relativity;

/// <summary>Двухчастичный распад в системе покоя исходной частицы</summary>
/// <param name="ParentRestEnergy">Энергия покоя распадающейся частицы</param>
/// <param name="FirstRestEnergy">Энергия покоя первого продукта</param>
/// <param name="SecondRestEnergy">Энергия покоя второго продукта</param>
/// <param name="Momentum">Импульс каждого продукта в единицах энергии, <c>pc</c></param>
/// <param name="FirstEnergy">Полная энергия первого продукта</param>
/// <param name="SecondEnergy">Полная энергия второго продукта</param>
public readonly record struct TwoBodyDecay(
    Quantity ParentRestEnergy,
    Quantity FirstRestEnergy,
    Quantity SecondRestEnergy,
    Quantity Momentum,
    Quantity FirstEnergy,
    Quantity SecondEnergy) : IInterpretable
{
    /// <summary>Энерговыделение: разность масс до и после, умноженная на c²</summary>
    public Quantity ReleasedEnergy
        => new(ParentRestEnergy.SiValue - FirstRestEnergy.SiValue - SecondRestEnergy.SiValue, Dimension.Energy);

    /// <summary>Кинетическая энергия первого продукта</summary>
    public Quantity FirstKineticEnergy => Kinetic(FirstEnergy, FirstRestEnergy);

    /// <summary>Кинетическая энергия второго продукта</summary>
    public Quantity SecondKineticEnergy => Kinetic(SecondEnergy, SecondRestEnergy);

    /// <summary>Скорость первого продукта в долях скорости света</summary>
    public double FirstBeta => Momentum.SiValue / FirstEnergy.SiValue;

    /// <summary>Скорость второго продукта в долях скорости света</summary>
    public double SecondBeta => Momentum.SiValue / SecondEnergy.SiValue;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double q = ReleasedEnergy.SiValue;
        double t1 = FirstKineticEnergy.SiValue;
        double t2 = SecondKineticEnergy.SiValue;
        bool unequal = Math.Abs(FirstRestEnergy.SiValue - SecondRestEnergy.SiValue) > 1e-9 * ParentRestEnergy.SiValue;
        double lighterShare = q > 0 ? Math.Max(t1, t2) / q : 0;

        return new InterpretationBuilder("Двухчастичный распад")
            .Summary($"Выделяется {PhysicsFormat.Energy(q)}. В системе покоя продукты разлетаются в противоположные "
                + $"стороны с одинаковым импульсом {PhysicsFormat.Energy(Momentum.SiValue)}/c; кинетическая энергия "
                + $"первого {PhysicsFormat.Energy(t1)}, второго {PhysicsFormat.Energy(t2)}.")
            .Metric("Энерговыделение", PhysicsFormat.Energy(q), null, "разность масс до и после, умноженная на c²")
            .Metric("Импульс продуктов", PhysicsFormat.Energy(Momentum.SiValue) + "/c", null, "одинаков у обоих")
            .Metric("Кинетическая энергия первого", PhysicsFormat.Energy(t1))
            .Metric("Кинетическая энергия второго", PhysicsFormat.Energy(t2))
            .Metric("β первого", FirstBeta, null, "скорость в долях скорости света", MetricQuality.Unknown, 5)
            .Metric("β второго", SecondBeta, null, "скорость в долях скорости света", MetricQuality.Unknown, 5)
            .FindingIf(unequal,
                $"Импульсы продуктов равны, а энергия делится неравно: лёгкий продукт уносит {Fmt.Pct(lighterShare)} "
                + "энерговыделения. Поэтому в альфа-распаде почти всё уносит альфа-частица, а отдача ядра — доли процента.")
            .Finding("Энергии продуктов двухчастичного распада однозначно заданы массами — спектр линейчатый. Когда "
                + "спектр электронов бета-распада оказался непрерывным, это выдало третью, невидимую частицу: так Паули "
                + "в 1930 году пришёл к нейтрино.")
            .Warning("Расчёт в системе покоя исходной частицы. В лаборатории энергии и углы продуктов зависят от её "
                + "скорости; перейти туда можно преобразованием Лоренца четырёхимпульсов — FourVector.Boost.")
            .Build();
    }

    // T = (pc)²/(E + mc²) — без вычитания близких чисел у тяжёлых медленных продуктов
    private Quantity Kinetic(Quantity energy, Quantity rest)
        => new(Momentum.SiValue * Momentum.SiValue / (energy.SiValue + rest.SiValue), Dimension.Energy);
}
