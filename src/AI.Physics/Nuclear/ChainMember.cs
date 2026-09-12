using AI.Units;

namespace AI.Physics.Nuclear;

/// <summary>Член цепочки распада</summary>
public sealed record ChainMember
{
    /// <summary>Создаёт член цепочки</summary>
    /// <param name="name">Обозначение нуклида</param>
    /// <param name="halfLife">Период полураспада; <c>null</c> — стабильный нуклид в конце цепочки</param>
    /// <param name="branchingToNext">
    /// Доля распадов, ведущих к следующему члену цепочки; остальные уходят в другие ветви
    /// </param>
    public ChainMember(string name, Quantity? halfLife, double branchingToNext = 1.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (halfLife is { } value)
            _ = RadioactiveDecay.Seconds(value, nameof(halfLife));

        if (branchingToNext is not (>= 0 and <= 1))
            throw new ArgumentOutOfRangeException(nameof(branchingToNext), "Доля ветвления лежит на отрезке [0, 1]");

        Name = name;
        HalfLife = halfLife;
        BranchingToNext = branchingToNext;
    }

    /// <summary>Обозначение нуклида</summary>
    public string Name { get; }

    /// <summary>Период полураспада; <c>null</c> у стабильного нуклида</summary>
    public Quantity? HalfLife { get; }

    /// <summary>Доля распадов, ведущих к следующему члену цепочки</summary>
    public double BranchingToNext { get; }

    /// <summary>Стабилен ли нуклид</summary>
    public bool IsStable => HalfLife is null;

    /// <summary>Стабильный нуклид — конец цепочки</summary>
    /// <param name="name">Обозначение</param>
    public static ChainMember Stable(string name) => new(name, null);
}
