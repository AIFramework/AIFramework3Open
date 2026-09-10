namespace AI.Simulation.Planning;

/// <summary>
/// Поиск плана A* в неявно заданном пространстве состояний.
/// </summary>
/// <remarks>
/// <para>
/// Состояния не перечисляются заранее: соседей порождает функция переходов по мере раскрытия.
/// Поэтому здесь не подходит <c>AStarSearch</c> из <c>AI.Algorithms</c> — он работает
/// на готовом графе с целочисленными вершинами, а построить такой граф значит заранее перебрать
/// все достижимые состояния, что и обесценивает A*.
/// </para>
/// <para>
/// Цель проверяется при извлечении состояния из очереди, а не при порождении: только так
/// найденный план оптимален при допустимой эвристике, то есть не переоценивающей остаток пути.
/// Состояние, до которого нашёлся более дешёвый путь, раскрывается повторно — это делает поиск
/// корректным и для допустимой, но не монотонной эвристики.
/// </para>
/// </remarks>
public static class StateSpaceSearch
{
    /// <summary>
    /// Ищет план наименьшей стоимости
    /// </summary>
    /// <typeparam name="TState">Тип состояния</typeparam>
    /// <param name="initial">Начальное состояние</param>
    /// <param name="isGoal">Является ли состояние целевым</param>
    /// <param name="successors">Доступные переходы: название действия, следующее состояние, стоимость</param>
    /// <param name="heuristic">Оценка стоимости остатка пути; по умолчанию нулевая — поиск по стоимости</param>
    /// <param name="admissibleHeuristic">Не переоценивает ли эвристика остаток пути</param>
    /// <param name="comparer">Сравнение состояний; по умолчанию стандартное</param>
    /// <param name="maxExpansions">Предел числа раскрытых состояний</param>
    public static Plan<TState> AStar<TState>(
        TState initial,
        Func<TState, bool> isGoal,
        Func<TState, IEnumerable<(string Action, TState Next, double Cost)>> successors,
        Func<TState, double>? heuristic = null,
        bool admissibleHeuristic = true,
        IEqualityComparer<TState>? comparer = null,
        int maxExpansions = 1_000_000)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(isGoal);
        ArgumentNullException.ThrowIfNull(successors);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExpansions);

        comparer ??= EqualityComparer<TState>.Default;
        heuristic ??= static _ => 0.0;

        // Равные оценки упорядочены по номеру постановки: результат не зависит от устройства очереди
        var open = new PriorityQueue<TState, (double Estimate, long Order)>(
            Comparer<(double Estimate, long Order)>.Create((left, right) =>
            {
                int byEstimate = left.Estimate.CompareTo(right.Estimate);

                return byEstimate != 0 ? byEstimate : left.Order.CompareTo(right.Order);
            }));

        var cost = new Dictionary<TState, double>(comparer) { [initial] = 0.0 };
        var parent = new Dictionary<TState, (TState Previous, string Action)>(comparer);
        var closed = new HashSet<TState>(comparer);
        long order = 0;
        int expanded = 0;

        open.Enqueue(initial, (heuristic(initial), order++));

        while (open.TryDequeue(out TState? state, out _))
        {
            if (!closed.Add(state))
                continue;

            if (isGoal(state))
                return Reconstruct(state, initial, cost[state], parent, comparer, expanded, admissibleHeuristic);

            expanded++;

            if (expanded >= maxExpansions)
                return new Plan<TState>(false, [], [], 0, expanded, LimitReached: true, Optimal: false);

            double reached = cost[state];

            foreach ((string action, TState next, double step) in successors(state))
            {
                if (step < 0)
                    throw new InvalidOperationException($"Стоимость действия «{action}» отрицательна: {step}");

                double candidate = reached + step;

                if (cost.TryGetValue(next, out double known) && known <= candidate)
                    continue;

                cost[next] = candidate;
                parent[next] = (state, action);
                _ = closed.Remove(next);
                open.Enqueue(next, (candidate + heuristic(next), order++));
            }
        }

        return new Plan<TState>(false, [], [], 0, expanded, LimitReached: false, Optimal: false);
    }

    private static Plan<TState> Reconstruct<TState>(
        TState goal,
        TState initial,
        double total,
        Dictionary<TState, (TState Previous, string Action)> parent,
        IEqualityComparer<TState> comparer,
        int expanded,
        bool admissible)
        where TState : notnull
    {
        var actions = new List<string>();
        var states = new List<TState> { goal };
        TState current = goal;

        while (!comparer.Equals(current, initial))
        {
            (TState previous, string action) = parent[current];
            actions.Add(action);
            states.Add(previous);
            current = previous;
        }

        actions.Reverse();
        states.Reverse();

        return new Plan<TState>(true, actions, states, total, expanded, LimitReached: false, Optimal: admissible);
    }
}
