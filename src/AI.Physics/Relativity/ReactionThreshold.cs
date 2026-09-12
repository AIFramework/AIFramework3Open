using AI.Insights;
using AI.Physics.Internal;
using AI.Units;

namespace AI.Physics.Relativity;

/// <summary>Порог реакции на неподвижной мишени</summary>
/// <param name="InitialRestEnergy">Суммарная энергия покоя налетающей частицы и мишени</param>
/// <param name="FinalRestEnergy">Суммарная энергия покоя продуктов</param>
/// <param name="ProjectileRestEnergy">Энергия покоя налетающей частицы</param>
/// <param name="TargetRestEnergy">Энергия покоя мишени</param>
/// <param name="KineticEnergy">Пороговая кинетическая энергия налетающей частицы</param>
public readonly record struct ReactionThreshold(
    Quantity InitialRestEnergy,
    Quantity FinalRestEnergy,
    Quantity ProjectileRestEnergy,
    Quantity TargetRestEnergy,
    Quantity KineticEnergy) : IInterpretable
{
    /// <summary>Энергия реакции Q: отрицательная у реакций, требующих энергии</summary>
    public Quantity QValue => new(InitialRestEnergy.SiValue - FinalRestEnergy.SiValue, Dimension.Energy);

    /// <summary>Требует ли реакция энергии извне</summary>
    public bool IsEndothermic => FinalRestEnergy.SiValue > InitialRestEnergy.SiValue;

    /// <summary>Классическая оценка порога <c>−Q(1 + m_a/m_b)</c></summary>
    public Quantity ClassicalEstimate => IsEndothermic
        ? new(-QValue.SiValue * (1 + (ProjectileRestEnergy.SiValue / TargetRestEnergy.SiValue)), Dimension.Energy)
        : Quantity.Zero(Dimension.Energy);

    /// <summary>
    /// Доля кинетической энергии пучка, ушедшая на рождение масс; остальное — движение центра масс
    /// </summary>
    public double MassCreationShare => IsEndothermic ? -QValue.SiValue / KineticEnergy.SiValue : 0;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        if (!IsEndothermic)
        {
            return new InterpretationBuilder("Порог реакции")
                .Summary($"Реакция выделяет энергию (Q = {PhysicsFormat.Energy(QValue.SiValue)}): порога по энергии нет.")
                .Metric("Q", PhysicsFormat.Energy(QValue.SiValue), null, "разность энергий покоя до и после")
                .Warning("Для заряженных частиц реакцию сдерживает ещё кулоновский барьер — эта формула его не учитывает, "
                    + "она говорит только о законах сохранения энергии и импульса.")
                .Build();
        }

        double threshold = KineticEnergy.SiValue;
        double classical = ClassicalEstimate.SiValue;
        double classicalError = (classical - threshold) / threshold;
        bool symmetric = Math.Abs(ProjectileRestEnergy.SiValue - TargetRestEnergy.SiValue)
            <= 1e-9 * TargetRestEnergy.SiValue;
        double colliderKinetic = (FinalRestEnergy.SiValue / 2) - ProjectileRestEnergy.SiValue;

        return new InterpretationBuilder("Порог реакции")
            .Summary($"Налетающей частице нужна кинетическая энергия не меньше {PhysicsFormat.Energy(threshold)}. "
                + $"На рождение масс из неё уходит {PhysicsFormat.Energy(-QValue.SiValue)} — {Fmt.Pct(MassCreationShare)}; "
                + "остальное остаётся движением центра масс.")
            .Metric("Пороговая энергия", PhysicsFormat.Energy(threshold), null, "кинетическая, на неподвижной мишени")
            .Metric("Q", PhysicsFormat.Energy(QValue.SiValue), null, "разность энергий покоя до и после")
            .Metric("Доля на рождение масс", Fmt.Pct(MassCreationShare), null, "остальное — движение центра масс")
            .Metric("Классическая оценка", PhysicsFormat.Energy(classical), null, "−Q(1 + m_a/m_b)")
            .FindingIf(Math.Abs(classicalError) <= 0.01,
                $"Классическая оценка совпадает с точной до {Fmt.Pct(Math.Abs(classicalError), 2)}: энергия реакции "
                + "мала по сравнению с массами, как в ядерных реакциях, и релятивистские поправки не нужны.")
            .FindingIf(Math.Abs(classicalError) > 0.01,
                $"Классическая оценка ошибается на {Fmt.Pct(Math.Abs(classicalError))}: энергия реакции сравнима с массами, "
                + "и без релятивистской кинематики порог не посчитать.")
            .FindingIf(symmetric,
                $"На встречных пучках тех же частиц хватило бы {PhysicsFormat.Energy(colliderKinetic)} кинетической "
                + "энергии на каждый пучок: центр масс покоится, и вся энергия идёт в дело. На неподвижной мишени √s растёт "
                + "лишь как корень из энергии пучка — поэтому ускорители перешли к встречным пучкам.")
            .Build();
    }
}
