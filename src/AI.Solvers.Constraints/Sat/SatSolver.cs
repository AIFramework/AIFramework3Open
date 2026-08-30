namespace AI.Solvers.Constraints.Sat;

/// <summary>Исход решения задачи выполнимости</summary>
public enum SatStatus
{
    /// <summary>Формула выполнима, найдена подстановка</summary>
    Satisfiable,

    /// <summary>Формула невыполнима — доказано</summary>
    Unsatisfiable,

    /// <summary>Исчерпан предел конфликтов: ответ неизвестен</summary>
    Unknown
}

/// <summary>Настройки решателя выполнимости</summary>
public sealed class SatOptions
{
    /// <summary>Предел числа конфликтов; ноль или меньше — без предела</summary>
    public int MaxConflicts { get; set; }

    /// <summary>Множитель роста активности переменных при конфликте</summary>
    public double VariableDecay { get; set; } = 0.95;

    /// <summary>Базовое число конфликтов между перезапусками (последовательность Луби)</summary>
    public int RestartBase { get; set; } = 100;
}

/// <summary>
/// Результат решения задачи выполнимости
/// </summary>
public sealed class SatSolution
{
    private readonly bool[] _model;

    internal SatSolution(SatStatus status, bool[] model, int decisions, int propagations, int conflicts, int learned)
    {
        Status = status;
        _model = model;
        Decisions = decisions;
        Propagations = propagations;
        Conflicts = conflicts;
        LearnedClauses = learned;
    }

    /// <summary>Исход</summary>
    public SatStatus Status { get; }

    /// <summary>Выполнима ли формула</summary>
    public bool IsSatisfiable => Status == SatStatus.Satisfiable;

    /// <summary>Число принятых решений</summary>
    public int Decisions { get; }

    /// <summary>Число выведенных значений</summary>
    public int Propagations { get; }

    /// <summary>Число конфликтов</summary>
    public int Conflicts { get; }

    /// <summary>Число выученных дизъюнктов</summary>
    public int LearnedClauses { get; }

    /// <summary>
    /// Значение переменной в найденной подстановке
    /// </summary>
    /// <param name="variable">Номер переменной, начиная с единицы</param>
    /// <exception cref="InvalidOperationException">Подстановки нет</exception>
    public bool this[int variable]
    {
        get
        {
            if (Status != SatStatus.Satisfiable)
                throw new InvalidOperationException("Подстановка есть только у выполнимой формулы");

            ArgumentOutOfRangeException.ThrowIfLessThan(variable, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(variable, _model.Length);

            return _model[variable - 1];
        }
    }

    /// <summary>Значения всех переменных; пустой массив, если подстановки нет</summary>
    public IReadOnlyList<bool> Model => _model;

    /// <summary>
    /// Проверяет, что подстановка действительно выполняет формулу.
    /// Дешёвая независимая проверка ответа решателя.
    /// </summary>
    /// <param name="formula">Исходная формула</param>
    public bool Verify(CnfFormula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);

        if (Status != SatStatus.Satisfiable)
            return false;

        foreach (int[] clause in formula.Clauses)
        {
            bool satisfied = false;

            foreach (int literal in clause)
            {
                bool value = _model[Math.Abs(literal) - 1];

                if (literal > 0 == value)
                {
                    satisfied = true;
                    break;
                }
            }

            if (!satisfied)
                return false;
        }

        return true;
    }

    /// <summary>Краткая запись результата</summary>
    public override string ToString() => Status switch
    {
        SatStatus.Satisfiable => $"выполнима: решений {Decisions}, конфликтов {Conflicts}",
        SatStatus.Unsatisfiable => $"невыполнима: конфликтов {Conflicts}",
        _ => "предел конфликтов исчерпан"
    };
}

/// <summary>
/// Решатель задачи выполнимости методом CDCL: обучение на конфликтах с обратным прыжком.
/// </summary>
/// <remarks>
/// <para>
/// Основные части ровно те, что делают метод работоспособным: распространение через
/// два наблюдаемых литерала, разбор конфликта до первой точки единственной импликации,
/// обратный прыжок вместо отката на один уровень, выбор переменной по накопленной
/// активности, сохранение фазы и перезапуски по последовательности Луби.
/// </para>
/// <para>
/// Выученные дизъюнкты не удаляются. На задачах в тысячи переменных это допустимо,
/// на больших промышленных — нет: там нужна периодическая чистка базы по мере полезности.
/// Предел конфликтов <see cref="SatOptions.MaxConflicts"/> не даёт решателю работать
/// неограниченно долго и возвращает <see cref="SatStatus.Unknown"/>.
/// </para>
/// <para>
/// Частный случай 2-КНФ решается за линейное время алгоритмом на графе импликаций —
/// <c>AI.Algorithms.GraphStructure.TwoSAT</c>. Для двухлитеральных задач он предпочтительнее.
/// </para>
/// </remarks>
public sealed class SatSolver
{
    private sealed class Clause(int[] literals, bool learnt)
    {
        internal readonly int[] Literals = literals;
        internal readonly bool Learnt = learnt;
    }

    private const int Undefined = 0;
    private const int True = 1;
    private const int False = -1;

    private readonly SatOptions _options;

    private int _variables;
    private List<Clause>[] _watches = [];
    private int[] _value = [];
    private int[] _level = [];
    private Clause?[] _reason = [];
    private double[] _activity = [];
    private bool[] _phase = [];
    private bool[] _seen = [];

    private readonly List<int> _trail = [];
    private readonly List<int> _trailLimits = [];
    private readonly List<Clause> _learned = [];

    private int _queueHead;
    private double _variableIncrement = 1.0;
    private int _decisions;
    private int _propagations;
    private int _conflicts;

    /// <summary>Создаёт решатель</summary>
    /// <param name="options">Настройки; по умолчанию стандартные</param>
    public SatSolver(SatOptions? options = null) => _options = options ?? new SatOptions();

    /// <summary>
    /// Решает задачу выполнимости
    /// </summary>
    /// <param name="formula">Формула в КНФ</param>
    public static SatSolution Solve(CnfFormula formula, SatOptions? options = null)
        => new SatSolver(options).Run(formula);

    private SatSolution Run(CnfFormula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);

        _variables = formula.VariableCount;
        Initialize();

        // Дизъюнкты добавляются на нулевом уровне: единичные сразу становятся фактами
        foreach (int[] clause in formula.Clauses)
        {
            if (!AddClause(clause))
                return Failure(SatStatus.Unsatisfiable);
        }

        if (Propagate() is not null)
            return Failure(SatStatus.Unsatisfiable);

        int restart = 0;

        while (true)
        {
            int budget = LubyLimit(++restart);
            SatStatus status = Search(budget);

            if (status != SatStatus.Unknown)
                return Finish(status);

            if (_options.MaxConflicts > 0 && _conflicts >= _options.MaxConflicts)
                return Failure(SatStatus.Unknown);
        }
    }

    #region Поиск

    private SatStatus Search(int conflictBudget)
    {
        int conflictsHere = 0;

        while (true)
        {
            Clause? conflict = Propagate();

            if (conflict is not null)
            {
                _conflicts++;
                conflictsHere++;

                if (DecisionLevel == 0)
                    return SatStatus.Unsatisfiable;

                (int[] learnt, int backjump) = Analyze(conflict);

                Backtrack(backjump);

                if (learnt.Length == 1)
                {
                    Enqueue(learnt[0], null);
                }
                else
                {
                    var clause = new Clause(learnt, learnt: true);
                    _learned.Add(clause);
                    Attach(clause);
                    Enqueue(learnt[0], clause);
                }

                _variableIncrement /= _options.VariableDecay;

                if (_options.MaxConflicts > 0 && _conflicts >= _options.MaxConflicts)
                    return SatStatus.Unknown;

                continue;
            }

            if (conflictBudget > 0 && conflictsHere >= conflictBudget)
            {
                Backtrack(0);
                return SatStatus.Unknown;
            }

            int next = ChooseVariable();

            if (next < 0)
                return SatStatus.Satisfiable;

            _decisions++;
            _trailLimits.Add(_trail.Count);
            Enqueue(Encode(next, _phase[next]), null);
        }
    }

    /// <summary>Переменная с наибольшей активностью среди неназначенных</summary>
    private int ChooseVariable()
    {
        int best = -1;
        double bestActivity = double.NegativeInfinity;

        for (int v = 0; v < _variables; v++)
        {
            if (_value[v] != Undefined)
                continue;

            if (_activity[v] > bestActivity)
            {
                bestActivity = _activity[v];
                best = v;
            }
        }

        return best;
    }

    #endregion

    #region Распространение

    private Clause? Propagate()
    {
        while (_queueHead < _trail.Count)
        {
            int literal = _trail[_queueHead++];
            int falsified = literal ^ 1;

            List<Clause> watchers = _watches[falsified];
            int kept = 0;

            for (int i = 0; i < watchers.Count; i++)
            {
                Clause clause = watchers[i];
                int[] literals = clause.Literals;

                // Наблюдаемый литерал приводится ко второй позиции
                if (literals[0] == falsified)
                    (literals[0], literals[1]) = (literals[1], literals[0]);

                if (Value(literals[0]) == True)
                {
                    watchers[kept++] = clause;
                    continue;
                }

                int replacement = -1;

                for (int k = 2; k < literals.Length; k++)
                {
                    if (Value(literals[k]) != False)
                    {
                        replacement = k;
                        break;
                    }
                }

                if (replacement >= 0)
                {
                    // Наблюдение переезжает на новый литерал: из этого списка дизъюнкт уходит
                    (literals[1], literals[replacement]) = (literals[replacement], literals[1]);
                    _watches[literals[1]].Add(clause);
                    continue;
                }

                watchers[kept++] = clause;

                if (Value(literals[0]) == False)
                {
                    // Конфликт: оставшиеся наблюдатели переносятся без изменений
                    for (int j = i + 1; j < watchers.Count; j++)
                        watchers[kept++] = watchers[j];

                    watchers.RemoveRange(kept, watchers.Count - kept);
                    _queueHead = _trail.Count;

                    return clause;
                }

                Enqueue(literals[0], clause);
            }

            watchers.RemoveRange(kept, watchers.Count - kept);
        }

        return null;
    }

    private void Enqueue(int literal, Clause? reason)
    {
        int variable = literal >> 1;

        _value[variable] = (literal & 1) == 0 ? True : False;
        _level[variable] = DecisionLevel;
        _reason[variable] = reason;
        _phase[variable] = (literal & 1) == 0;

        _trail.Add(literal);
        _propagations++;
    }

    #endregion

    #region Разбор конфликта

    /// <summary>
    /// Разбор конфликта до первой точки единственной импликации.
    /// </summary>
    /// <returns>Выученный дизъюнкт и уровень обратного прыжка</returns>
    private (int[] Learnt, int Backjump) Analyze(Clause conflict)
    {
        var learnt = new List<int> { 0 };  // нулевая позиция под точку импликации
        Array.Clear(_seen);

        int counter = 0;
        int index = _trail.Count - 1;
        int uip = 0;
        Clause? clause = conflict;
        bool atConflict = true;

        do
        {
            // У дизъюнкта-причины нулевой литерал — это сама выведенная переменная,
            // её пересматривать нельзя: иначе счётчик уровня никогда не дойдёт до нуля
            int start = atConflict ? 0 : 1;
            atConflict = false;

            for (int position = start; position < clause!.Literals.Length; position++)
            {
                int literal = clause.Literals[position];
                int variable = literal >> 1;

                if (_seen[variable] || _level[variable] == 0)
                    continue;

                _seen[variable] = true;
                BumpActivity(variable);

                if (_level[variable] >= DecisionLevel)
                    counter++;
                else
                    learnt.Add(literal);
            }

            while (!_seen[_trail[index] >> 1])
                index--;

            uip = _trail[index--];
            clause = _reason[uip >> 1];
            _seen[uip >> 1] = false;
            counter--;
        }
        while (counter > 0);

        learnt[0] = uip ^ 1;

        if (learnt.Count == 1)
            return (learnt.ToArray(), 0);

        // Второй по уровню литерал ставится на первую позицию: он определяет прыжок
        int best = 1;

        for (int i = 2; i < learnt.Count; i++)
            if (_level[learnt[i] >> 1] > _level[learnt[best] >> 1])
                best = i;

        (learnt[1], learnt[best]) = (learnt[best], learnt[1]);

        return (learnt.ToArray(), _level[learnt[1] >> 1]);
    }

    private void BumpActivity(int variable)
    {
        _activity[variable] += _variableIncrement;

        if (_activity[variable] <= 1e100)
            return;

        for (int v = 0; v < _variables; v++)
            _activity[v] *= 1e-100;

        _variableIncrement *= 1e-100;
    }

    #endregion

    #region Служебное

    private int DecisionLevel => _trailLimits.Count;

    private void Initialize()
    {
        _watches = new List<Clause>[2 * _variables];

        for (int i = 0; i < _watches.Length; i++)
            _watches[i] = [];

        _value = new int[_variables];
        _level = new int[_variables];
        _reason = new Clause?[_variables];
        _activity = new double[_variables];
        _phase = new bool[_variables];
        _seen = new bool[_variables];

        _trail.Clear();
        _trailLimits.Clear();
        _learned.Clear();
        _queueHead = 0;
    }

    /// <summary>Добавляет исходный дизъюнкт; <c>false</c> означает немедленную невыполнимость</summary>
    private bool AddClause(int[] dimacs)
    {
        var literals = new List<int>(dimacs.Length);

        foreach (int literal in dimacs)
        {
            int encoded = Encode(Math.Abs(literal) - 1, literal > 0);

            if (Value(encoded) == True && _level[encoded >> 1] == 0)
                return true;   // дизъюнкт уже выполнен фактом

            if (Value(encoded) == False && _level[encoded >> 1] == 0)
                continue;      // литерал заведомо ложен и не участвует

            if (literals.Contains(encoded ^ 1))
                return true;   // дизъюнкт тривиально истинен

            if (!literals.Contains(encoded))
                literals.Add(encoded);
        }

        if (literals.Count == 0)
            return false;

        if (literals.Count == 1)
        {
            Enqueue(literals[0], null);
            return true;
        }

        var clause = new Clause(literals.ToArray(), learnt: false);
        Attach(clause);

        return true;
    }

    private void Attach(Clause clause)
    {
        _watches[clause.Literals[0]].Add(clause);
        _watches[clause.Literals[1]].Add(clause);
    }

    private void Backtrack(int level)
    {
        if (DecisionLevel <= level)
            return;

        int bound = _trailLimits[level];

        for (int i = _trail.Count - 1; i >= bound; i--)
        {
            int variable = _trail[i] >> 1;
            _value[variable] = Undefined;
            _reason[variable] = null;
        }

        _trail.RemoveRange(bound, _trail.Count - bound);
        _trailLimits.RemoveRange(level, _trailLimits.Count - level);
        _queueHead = _trail.Count;
    }

    /// <summary>Значение литерала: истина, ложь либо не определено</summary>
    private int Value(int literal)
    {
        int variable = _value[literal >> 1];

        return variable == Undefined ? Undefined : (literal & 1) == 0 ? variable : -variable;
    }

    private static int Encode(int variable, bool positive) => positive ? variable << 1 : (variable << 1) | 1;

    /// <summary>
    /// Предел конфликтов очередного захода по последовательности Луби: 1, 1, 2, 1, 1, 2, 4, …
    /// </summary>
    /// <remarks>
    /// Последовательность даёт и частые короткие заходы, вытаскивающие решатель из неудачной
    /// ветви, и редкие длинные, позволяющие довести трудный поиск до конца.
    /// </remarks>
    /// <param name="restart">Номер захода, начиная с единицы</param>
    private int LubyLimit(int restart)
    {
        int size = 1;
        int sequence = 0;

        while (size < restart + 1)
        {
            sequence++;
            size = (2 * size) + 1;
        }

        while (size - 1 != restart)
        {
            size = (size - 1) >> 1;
            sequence--;
            restart %= size;
        }

        return _options.RestartBase * (1 << sequence);
    }

    private SatSolution Failure(SatStatus status)
        => new(status, [], _decisions, _propagations, _conflicts, _learned.Count);

    private SatSolution Finish(SatStatus status)
    {
        if (status != SatStatus.Satisfiable)
            return Failure(status);

        var model = new bool[_variables];

        for (int v = 0; v < _variables; v++)
            model[v] = _value[v] != False;

        return new SatSolution(SatStatus.Satisfiable, model, _decisions, _propagations, _conflicts, _learned.Count);
    }

    #endregion
}
