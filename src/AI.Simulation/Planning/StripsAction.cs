namespace AI.Simulation.Planning;

/// <summary>
/// Действие в STRIPS: применимо, если истинны все предусловия; удаляет одни факты и добавляет другие
/// </summary>
public sealed class StripsAction
{
    /// <summary>Создаёт действие</summary>
    /// <param name="name">Название</param>
    /// <param name="preconditions">Факты, которые должны быть истинны</param>
    /// <param name="add">Факты, которые становятся истинными</param>
    /// <param name="delete">Факты, которые перестают быть истинными</param>
    /// <param name="cost">Стоимость действия</param>
    public StripsAction(
        string name,
        IEnumerable<string> preconditions,
        IEnumerable<string> add,
        IEnumerable<string> delete,
        double cost = 1.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(preconditions);
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(delete);

        if (cost < 0)
            throw new ArgumentOutOfRangeException(nameof(cost), "Стоимость действия не может быть отрицательной");

        Name = name;
        Preconditions = new HashSet<string>(preconditions, StringComparer.Ordinal);
        Add = new HashSet<string>(add, StringComparer.Ordinal);
        Delete = new HashSet<string>(delete, StringComparer.Ordinal);
        Cost = cost;
    }

    /// <summary>Название</summary>
    public string Name { get; }

    /// <summary>Факты, которые должны быть истинны</summary>
    public IReadOnlySet<string> Preconditions { get; }

    /// <summary>Факты, которые становятся истинными</summary>
    public IReadOnlySet<string> Add { get; }

    /// <summary>Факты, которые перестают быть истинными</summary>
    public IReadOnlySet<string> Delete { get; }

    /// <summary>Стоимость</summary>
    public double Cost { get; }

    /// <summary>Применимо ли действие в состоянии</summary>
    /// <param name="state">Состояние</param>
    public bool IsApplicable(FactState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Preconditions.All(state.Contains);
    }

    /// <summary>
    /// Результат действия: сначала удаляются факты, затем добавляются — поэтому факт,
    /// который действие и удаляет, и добавляет, остаётся истинным
    /// </summary>
    /// <param name="state">Состояние</param>
    public FactState Apply(FactState state)
    {
        if (!IsApplicable(state))
            throw new InvalidOperationException($"Действие «{Name}» неприменимо в состоянии {state}");

        return new FactState(state.Facts.Where(f => !Delete.Contains(f)).Concat(Add));
    }

    /// <summary>Запись действия</summary>
    public override string ToString() => Name;
}
