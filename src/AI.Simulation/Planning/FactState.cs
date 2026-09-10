namespace AI.Simulation.Planning;

/// <summary>
/// Состояние мира в STRIPS: множество истинных фактов
/// </summary>
/// <remarks>
/// Равенство — по составу фактов, без учёта порядка; хеш вычисляется один раз при создании,
/// потому что состояние многократно ищется в таблицах поиска.
/// </remarks>
public sealed class FactState : IEquatable<FactState>
{
    private readonly string[] _sorted;
    private readonly HashSet<string> _facts;
    private readonly int _hash;

    /// <summary>Создаёт состояние</summary>
    /// <param name="facts">Истинные факты</param>
    public FactState(IEnumerable<string> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        _facts = new HashSet<string>(facts, StringComparer.Ordinal);
        _sorted = _facts.OrderBy(f => f, StringComparer.Ordinal).ToArray();

        var hash = new HashCode();

        foreach (string fact in _sorted)
            hash.Add(fact, StringComparer.Ordinal);

        _hash = hash.ToHashCode();
    }

    /// <summary>Истинные факты</summary>
    public IReadOnlySet<string> Facts => _facts;

    /// <summary>Истинен ли факт</summary>
    /// <param name="fact">Факт</param>
    public bool Contains(string fact) => _facts.Contains(fact);

    /// <inheritdoc />
    public bool Equals(FactState? other)
        => other is not null && _hash == other._hash && _sorted.SequenceEqual(other._sorted, StringComparer.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FactState);

    /// <inheritdoc />
    public override int GetHashCode() => _hash;

    /// <summary>Запись состояния</summary>
    public override string ToString() => "{" + string.Join(", ", _sorted) + "}";
}
