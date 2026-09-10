namespace AI.Simulation.Planning;

/// <summary>
/// Задача планирования в STRIPS: начальные факты, цель и действия.
/// </summary>
/// <remarks>
/// <para>
/// Мир описан фактами, действие — предусловиями и изменениями. Такое описание отвечает на
/// вопрос «что сделать, чтобы стало так», тогда как симуляция отвечает на «что будет, если».
/// </para>
/// <para>
/// Эвристика — число невыполненных целевых фактов, делённое на наибольшее число целевых фактов,
/// которые может добавить одно действие, и умноженное на наименьшую стоимость действия. Она
/// допустима: быстрее закрыть недостающие цели нельзя, — поэтому найденный план оптимален.
/// Простой счёт невыполненных целей допустимым не был бы: одно действие может закрыть сразу
/// несколько целей, и такая оценка переоценила бы остаток пути.
/// </para>
/// </remarks>
public sealed class StripsProblem
{
    private readonly HashSet<string> _goal;

    /// <summary>Создаёт задачу</summary>
    /// <param name="initial">Истинные факты в начале</param>
    /// <param name="goal">Факты, которые должны стать истинными</param>
    /// <param name="actions">Доступные действия</param>
    public StripsProblem(IEnumerable<string> initial, IEnumerable<string> goal, IEnumerable<StripsAction> actions)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(actions);

        Initial = new FactState(initial);
        _goal = new HashSet<string>(goal, StringComparer.Ordinal);
        Actions = actions.ToList();
    }

    /// <summary>Начальное состояние</summary>
    public FactState Initial { get; }

    /// <summary>Цель</summary>
    public IReadOnlySet<string> Goal => _goal;

    /// <summary>Доступные действия</summary>
    public IReadOnlyList<StripsAction> Actions { get; }

    /// <summary>Достигнута ли цель в состоянии</summary>
    /// <param name="state">Состояние</param>
    public bool IsGoal(FactState state) => _goal.All(state.Contains);

    /// <summary>
    /// Ищет оптимальный план
    /// </summary>
    /// <param name="maxExpansions">Предел числа раскрытых состояний</param>
    public Plan<FactState> Solve(int maxExpansions = 1_000_000)
    {
        int achievable = Actions.Count == 0
            ? 1
            : Math.Max(1, Actions.Max(a => a.Add.Count(_goal.Contains)));

        double cheapest = Actions.Count == 0 ? 0 : Actions.Min(a => a.Cost);

        double Heuristic(FactState state)
        {
            int missing = _goal.Count(f => !state.Contains(f));

            return Math.Ceiling((double)missing / achievable) * cheapest;
        }

        IEnumerable<(string, FactState, double)> Successors(FactState state)
        {
            foreach (StripsAction action in Actions)
            {
                if (action.IsApplicable(state))
                    yield return (action.Name, action.Apply(state), action.Cost);
            }
        }

        return StateSpaceSearch.AStar(Initial, IsGoal, Successors, Heuristic,
            admissibleHeuristic: true, maxExpansions: maxExpansions);
    }
}
