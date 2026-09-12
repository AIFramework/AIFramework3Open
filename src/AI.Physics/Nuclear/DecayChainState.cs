using AI.Insights;
using AI.Physics.Internal;
using AI.Units;

namespace AI.Physics.Nuclear;

/// <summary>Состояние цепочки распада в заданный момент</summary>
/// <param name="Time">Время от начала</param>
/// <param name="Members">Члены цепочки</param>
/// <param name="Amounts">Число ядер каждого члена</param>
/// <param name="Activities">Активность каждого члена</param>
/// <param name="Equilibria">Вид равновесия в каждой паре «член — следующий»</param>
public sealed record DecayChainState(
    Quantity Time,
    IReadOnlyList<ChainMember> Members,
    IReadOnlyList<double> Amounts,
    IReadOnlyList<Quantity> Activities,
    IReadOnlyList<DecayEquilibrium> Equilibria) : IInterpretable
{
    /// <summary>Суммарная активность цепочки</summary>
    public Quantity TotalActivity => new(Activities.Sum(a => a.SiValue), Dimension.Frequency);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var builder = new InterpretationBuilder("Цепочка радиоактивного распада")
            .Summary($"Через {PhysicsFormat.Duration(Time.SiValue)} суммарная активность цепочки "
                + $"{PhysicsFormat.Scientific(TotalActivity.SiValue)} Бк.");

        for (int i = 0; i < Math.Min(Members.Count, 12); i++)
        {
            builder = builder.Metric(
                Members[i].Name,
                Members[i].IsStable ? "стабилен" : PhysicsFormat.Scientific(Activities[i].SiValue) + " Бк",
                null,
                PhysicsFormat.Scientific(Amounts[i]) + " ядер");
        }

        for (int i = 0; i < Equilibria.Count; i++)
        {
            ChainMember parent = Members[i];
            ChainMember daughter = Members[i + 1];
            double parentActivity = Activities[i].SiValue;

            if (daughter.IsStable || parentActivity <= 0)
                continue;

            double parentLambda = Lambda(parent);
            double daughterLambda = Lambda(daughter);
            double ratio = Activities[i + 1].SiValue / parentActivity;

            builder = builder.Metric($"{daughter.Name} / {parent.Name}", Fmt.Num(ratio, 4), null, "отношение активностей");

            if (Equilibria[i] == DecayEquilibrium.None)
            {
                builder = builder.Finding($"{daughter.Name} живёт дольше, чем {parent.Name}: равновесия нет, "
                    + $"{daughter.Name} накапливается и остаётся после распада {parent.Name}.");
                continue;
            }

            double expected = parent.BranchingToNext * daughterLambda / (daughterLambda - parentLambda);
            bool reached = Math.Abs((ratio / expected) - 1) < 0.01;
            double daughterHalfLife = Math.Log(2) / daughterLambda;
            string wait = $"{PhysicsFormat.Duration(7 * daughterHalfLife)}–{PhysicsFormat.Duration(10 * daughterHalfLife)}";

            if (Equilibria[i] == DecayEquilibrium.Secular)
            {
                builder = reached
                    ? builder.Finding($"Между {parent.Name} и {daughter.Name} вековое равновесие: активности сравнялись"
                        + (parent.BranchingToNext < 1 ? " с поправкой на долю ветвления" : string.Empty)
                        + $". Поэтому активность долгоживущего {parent.Name} можно измерять по {daughter.Name}.")
                    : builder.Finding($"{daughter.Name} ещё не пришёл в вековое равновесие с {parent.Name}: для этого "
                        + $"нужно семь–десять его периодов полураспада, {wait}.");
            }
            else
            {
                builder = reached
                    ? builder.Finding($"Между {parent.Name} и {daughter.Name} подвижное равновесие: {daughter.Name} "
                        + $"спадает вместе с {parent.Name}, а отношение их активностей держится около {Fmt.Num(expected, 3)}.")
                    : builder.Finding($"Подвижное равновесие между {parent.Name} и {daughter.Name} ещё не установилось: "
                        + $"отношение активностей идёт к {Fmt.Num(expected, 3)}, на это нужно около {wait}.");
            }
        }

        return builder
            .Warning("Периоды полураспада и доли ветвления — входные данные, и результат не точнее их. Таблицы нуклидов "
                + "в модуле нет: значения берутся из NUBASE или ENSDF.")
            .Build();
    }

    private static double Lambda(ChainMember member)
        => member.HalfLife is { } halfLife ? Math.Log(2) / halfLife.SiValue : 0;
}
