namespace AI.Solvers.Constraints.Cp;

/// <summary>
/// Области значений переменных с возможностью отката.
/// </summary>
/// <remarks>
/// Удаления записываются в общий журнал; откат восстанавливает состояние до метки,
/// поэтому поиск не копирует области при каждом ветвлении.
/// </remarks>
public sealed class DomainStore
{
    private readonly bool[][] _alive;
    private readonly int[] _counts;
    private readonly int[] _lower;
    private readonly List<(int Variable, int Offset)> _journal = [];

    internal DomainStore(IReadOnlyList<IntVariable> variables)
    {
        _alive = new bool[variables.Count][];
        _counts = new int[variables.Count];
        _lower = new int[variables.Count];

        for (int i = 0; i < variables.Count; i++)
        {
            IntVariable variable = variables[i];

            _alive[i] = new bool[variable.Size];
            Array.Fill(_alive[i], true);
            _counts[i] = variable.Size;
            _lower[i] = variable.Lower;
        }
    }

    /// <summary>Метка для последующего отката</summary>
    internal int Mark => _journal.Count;

    /// <summary>Откатывает удаления, сделанные после метки</summary>
    internal void Undo(int mark)
    {
        for (int i = _journal.Count - 1; i >= mark; i--)
        {
            (int variable, int offset) = _journal[i];
            _alive[variable][offset] = true;
            _counts[variable]++;
        }

        _journal.RemoveRange(mark, _journal.Count - mark);
    }

    /// <summary>Число оставшихся значений</summary>
    /// <param name="variable">Переменная</param>
    public int Count(IntVariable variable) => _counts[variable.Index];

    /// <summary>Зафиксировано ли единственное значение</summary>
    /// <param name="variable">Переменная</param>
    /// <param name="value">Значение, если оно единственное</param>
    public bool TryGetFixed(IntVariable variable, out int value)
    {
        value = 0;

        if (_counts[variable.Index] != 1)
            return false;

        value = Minimum(variable);

        return true;
    }

    /// <summary>Наименьшее оставшееся значение</summary>
    /// <param name="variable">Переменная</param>
    public int Minimum(IntVariable variable)
    {
        bool[] alive = _alive[variable.Index];

        for (int offset = 0; offset < alive.Length; offset++)
            if (alive[offset])
                return _lower[variable.Index] + offset;

        return _lower[variable.Index];
    }

    /// <summary>Наибольшее оставшееся значение</summary>
    /// <param name="variable">Переменная</param>
    public int Maximum(IntVariable variable)
    {
        bool[] alive = _alive[variable.Index];

        for (int offset = alive.Length - 1; offset >= 0; offset--)
            if (alive[offset])
                return _lower[variable.Index] + offset;

        return _lower[variable.Index];
    }

    /// <summary>Оставшиеся значения</summary>
    /// <param name="variable">Переменная</param>
    public IEnumerable<int> Values(IntVariable variable)
    {
        bool[] alive = _alive[variable.Index];

        for (int offset = 0; offset < alive.Length; offset++)
            if (alive[offset])
                yield return _lower[variable.Index] + offset;
    }

    /// <summary>Содержит ли область значение</summary>
    /// <param name="variable">Переменная</param>
    /// <param name="value">Значение</param>
    public bool Contains(IntVariable variable, int value)
    {
        int offset = value - _lower[variable.Index];
        bool[] alive = _alive[variable.Index];

        return offset >= 0 && offset < alive.Length && alive[offset];
    }

    /// <summary>
    /// Удаляет значение; <c>false</c> означает опустевшую область
    /// </summary>
    /// <param name="variable">Переменная</param>
    /// <param name="value">Значение</param>
    public bool Remove(IntVariable variable, int value)
    {
        int index = variable.Index;
        int offset = value - _lower[index];
        bool[] alive = _alive[index];

        if (offset < 0 || offset >= alive.Length || !alive[offset])
            return _counts[index] > 0;

        alive[offset] = false;
        _counts[index]--;
        _journal.Add((index, offset));

        return _counts[index] > 0;
    }

    /// <summary>Удаляет значения больше предела</summary>
    /// <param name="variable">Переменная</param>
    /// <param name="limit">Предел</param>
    public bool RemoveAbove(IntVariable variable, int limit)
    {
        for (int value = Maximum(variable); value > limit; value--)
            if (!Remove(variable, value))
                return false;

        return _counts[variable.Index] > 0;
    }

    /// <summary>Удаляет значения меньше предела</summary>
    /// <param name="variable">Переменная</param>
    /// <param name="limit">Предел</param>
    public bool RemoveBelow(IntVariable variable, int limit)
    {
        for (int value = Minimum(variable); value < limit; value++)
            if (!Remove(variable, value))
                return false;

        return _counts[variable.Index] > 0;
    }

    /// <summary>Оставляет единственное значение</summary>
    /// <param name="variable">Переменная</param>
    /// <param name="value">Значение</param>
    public bool Assign(IntVariable variable, int value)
    {
        if (!Contains(variable, value))
            return false;

        foreach (int other in Values(variable).ToArray())
        {
            if (other == value)
                continue;

            if (!Remove(variable, other))
                return false;
        }

        return true;
    }
}

/// <summary>Исход решения задачи с ограничениями</summary>
public enum CpStatus
{
    /// <summary>Решение найдено</summary>
    Satisfiable,

    /// <summary>Решений нет — доказано перебором с распространением</summary>
    Infeasible,

    /// <summary>Исчерпан предел узлов: ответ неизвестен</summary>
    LimitReached
}

/// <summary>Настройки решателя</summary>
public sealed class CpOptions
{
    /// <summary>Сколько решений искать; по умолчанию одно</summary>
    public int SolutionLimit { get; set; } = 1;

    /// <summary>Предел числа узлов перебора</summary>
    public int MaxNodes { get; set; } = 5_000_000;
}

/// <summary>Решение задачи с ограничениями</summary>
public sealed class CpSolution
{
    private readonly List<int[]> _solutions;
    private readonly CpModel _model;

    internal CpSolution(CpModel model, CpStatus status, List<int[]> solutions, int nodes, int propagations)
    {
        _model = model;
        _solutions = solutions;

        Status = status;
        Nodes = nodes;
        Propagations = propagations;
    }

    /// <summary>Исход</summary>
    public CpStatus Status { get; }

    /// <summary>Найдено ли хотя бы одно решение</summary>
    public bool IsSatisfiable => Status == CpStatus.Satisfiable;

    /// <summary>Сколько решений найдено</summary>
    public int Count => _solutions.Count;

    /// <summary>Число узлов перебора</summary>
    public int Nodes { get; }

    /// <summary>Сколько раз выполнялось распространение ограничений</summary>
    public int Propagations { get; }

    /// <summary>Все найденные решения</summary>
    public IReadOnlyList<IReadOnlyList<int>> Solutions => _solutions;

    /// <summary>Значение переменной в первом найденном решении</summary>
    /// <param name="variable">Переменная</param>
    public int this[IntVariable variable]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(variable);

            return _solutions.Count == 0
                ? throw new InvalidOperationException("Решений не найдено")
                : _solutions[0][variable.Index];
        }
    }

    /// <summary>Значение переменной по имени в первом найденном решении</summary>
    /// <param name="name">Имя переменной</param>
    public int this[string name]
    {
        get
        {
            IntVariable variable = _model.Variables.FirstOrDefault(v => v.Name == name)
                ?? throw new KeyNotFoundException($"Переменной «{name}» в модели нет");

            return this[variable];
        }
    }

    /// <summary>Краткая запись результата</summary>
    public override string ToString() => Status switch
    {
        CpStatus.Satisfiable => $"решений найдено {Count}, узлов {Nodes}",
        CpStatus.Infeasible => $"решений нет, узлов {Nodes}",
        _ => $"предел узлов исчерпан, найдено {Count}"
    };
}

/// <summary>
/// Решатель задач удовлетворения ограничений: распространение по областям значений
/// и поиск в глубину с выбором самой стеснённой переменной.
/// </summary>
/// <remarks>
/// <para>
/// На каждом узле ограничения применяются до неподвижной точки: пока хотя бы одно из них
/// сужает чью-то область, круг повторяется. Пустая область означает тупик, и ветвь
/// отбрасывается без перебора её продолжений.
/// </para>
/// <para>
/// Переменная для ветвления выбирается по правилу первой неудачи — берётся с наименьшей
/// областью. Это не эвристика вкуса: чем меньше значений осталось, тем раньше вскроется
/// противоречие и тем меньше окажется бесплодное поддерево.
/// </para>
/// </remarks>
public static class CpSolver
{
    /// <summary>
    /// Решает задачу
    /// </summary>
    /// <param name="model">Модель</param>
    /// <param name="options">Настройки; по умолчанию ищется одно решение</param>
    public static CpSolution Solve(CpModel model, CpOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        options ??= new CpOptions();

        var domains = new DomainStore(model.Variables);
        var solutions = new List<int[]>();
        var state = new SearchState(model, options, domains, solutions);

        if (!Propagate(state))
            return new CpSolution(model, CpStatus.Infeasible, solutions, state.Nodes, state.Propagations);

        bool exhausted = Search(state);

        CpStatus status = solutions.Count > 0
            ? CpStatus.Satisfiable
            : exhausted ? CpStatus.Infeasible : CpStatus.LimitReached;

        if (solutions.Count > 0 && !exhausted && solutions.Count < options.SolutionLimit)
            status = CpStatus.LimitReached;

        return new CpSolution(model, status, solutions, state.Nodes, state.Propagations);
    }

    /// <summary>
    /// Перечисляет все решения задачи
    /// </summary>
    /// <param name="model">Модель</param>
    /// <param name="limit">Предел числа решений</param>
    public static CpSolution SolveAll(CpModel model, int limit = int.MaxValue)
        => Solve(model, new CpOptions { SolutionLimit = limit });

    private sealed class SearchState(CpModel model, CpOptions options, DomainStore domains, List<int[]> solutions)
    {
        internal readonly CpModel Model = model;
        internal readonly CpOptions Options = options;
        internal readonly DomainStore Domains = domains;
        internal readonly List<int[]> Solutions = solutions;
        internal int Nodes;
        internal int Propagations;
    }

    /// <summary>Возвращает <c>true</c>, если поддерево пройдено целиком</summary>
    private static bool Search(SearchState state)
    {
        if (state.Nodes >= state.Options.MaxNodes)
            return false;

        state.Nodes++;

        IntVariable? branching = ChooseVariable(state);

        if (branching is null)
        {
            var solution = new int[state.Model.Variables.Count];

            foreach (IntVariable variable in state.Model.Variables)
                solution[variable.Index] = state.Domains.Minimum(variable);

            state.Solutions.Add(solution);

            return state.Solutions.Count < state.Options.SolutionLimit;
        }

        foreach (int value in state.Domains.Values(branching).ToArray())
        {
            int mark = state.Domains.Mark;

            if (state.Domains.Assign(branching, value) && Propagate(state) && !Search(state))
            {
                state.Domains.Undo(mark);
                return false;
            }

            state.Domains.Undo(mark);

            if (state.Nodes >= state.Options.MaxNodes)
                return false;
        }

        return true;
    }

    /// <summary>Переменная с наименьшей областью среди незафиксированных</summary>
    private static IntVariable? ChooseVariable(SearchState state)
    {
        IntVariable? chosen = null;
        int smallest = int.MaxValue;

        foreach (IntVariable variable in state.Model.Variables)
        {
            int count = state.Domains.Count(variable);

            if (count <= 1 || count >= smallest)
                continue;

            smallest = count;
            chosen = variable;
        }

        return chosen;
    }

    /// <summary>Применяет ограничения до неподвижной точки</summary>
    private static bool Propagate(SearchState state)
    {
        bool changed = true;

        while (changed)
        {
            changed = false;

            foreach (IConstraint constraint in state.Model.Constraints)
            {
                int before = TotalSize(state);
                state.Propagations++;

                if (!constraint.Propagate(state.Domains))
                    return false;

                if (TotalSize(state) != before)
                    changed = true;
            }
        }

        return true;
    }

    private static int TotalSize(SearchState state)
    {
        int total = 0;

        foreach (IntVariable variable in state.Model.Variables)
            total += state.Domains.Count(variable);

        return total;
    }
}
