namespace AI.LLM.Agents.Planning;

/// <summary>
/// Отслеживает выполнение шагов плана: успехи, повторные попытки, провалы.
/// Подписчик агента сообщает об успехе или провале через
/// <see cref="TryMarkDone"/> / <see cref="TryMarkFailed"/>.
/// </summary>
public sealed class PlanTracker
{
    /// <summary>Сколько провалов одного шага считается «исчерпанным» и требует переплана.</summary>
    public const int MaxRetries = 2;

    private readonly List<StepStatus> _steps;
    private int _lastChangedIndex = -1;

    /// <summary>Есть ли шаг, исчерпавший все попытки и ожидающий перепланирования.</summary>
    public bool HasExhaustedStep => _steps.Any(s => s.Exhausted);

    /// <summary>Создаёт трекер для шагов плана.</summary>
    public PlanTracker(IEnumerable<PlanStep> steps)
    {
        _steps = steps.Select(s => new StepStatus(s)).ToList();
    }

    /// <summary>
    /// Помечает первый незавершённый шаг с совпадающим инструментом как выполненный.
    /// </summary>
    public bool TryMarkDone(string toolName)
    {
        var index = FindPending(toolName);
        if (index < 0) return false;

        _steps[index].Done = true;
        _lastChangedIndex = index;
        return true;
    }

    /// <summary>
    /// Регистрирует провал первого незавершённого шага с совпадающим инструментом.
    /// Если попытки исчерпаны — шаг помечается исчерпанным.
    /// </summary>
    public bool TryMarkFailed(string toolName)
    {
        var index = FindPending(toolName);
        if (index < 0) return false;

        var s = _steps[index];
        s.Retries++;
        if (s.Retries >= MaxRetries)
            s.Exhausted = true;
        _lastChangedIndex = index;
        return true;
    }

    /// <summary>Сбрасывает трекер при перепланировании.</summary>
    public void Reset(IEnumerable<PlanStep> newSteps)
    {
        _steps.Clear();
        _steps.AddRange(newSteps.Select(s => new StepStatus(s)));
        _lastChangedIndex = -1;
    }

    /// <summary>Печатает прогресс плана в консоль.</summary>
    public void PrintProgress()
    {
        var done  = _steps.Count(s => s.Done);
        var total = _steps.Count;

        Console.WriteLine();
        Console.WriteLine($"┌─── Plan Progress ({done}/{total}) ─────────────────────────");
        foreach (var (s, i) in _steps.Select((s, i) => (s, i)))
        {
            string marker;
            if      (s.Done)                  marker = "✓";
            else if (s.Exhausted)             marker = "✗";
            else if (s.Retries > 0)           marker = $"↺{s.Retries}";
            else if (i == _lastChangedIndex + 1) marker = "→";
            else                               marker = " ";

            var toolTag = s.Step.ToolName != null ? $" [{s.Step.ToolName}]" : "";
            Console.WriteLine($"│ {marker,-2} {s.Step.Id}{toolTag}: {s.Step.Description}");
        }
        Console.WriteLine("└───────────────────────────────────────────────────────");
        Console.WriteLine();
    }

    /// <summary>Индекс первого незавершённого шага с этим инструментом; -1 — нет такого.</summary>
    private int FindPending(string toolName)
    {
        if (IsMetaTool(toolName)) return -1;

        return _steps.FindIndex(s => !s.Done && !s.Exhausted &&
            string.Equals(s.Step.ToolName, toolName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMetaTool(string name) =>
        string.IsNullOrEmpty(name) ||
        name.Equals(PlanTool.PlanToolName,   StringComparison.OrdinalIgnoreCase) ||
        name.Equals(PlanTool.ReplanToolName, StringComparison.OrdinalIgnoreCase);

    private sealed class StepStatus(PlanStep step)
    {
        public PlanStep Step     { get; } = step;
        public bool     Done     { get; set; }
        public bool     Exhausted{ get; set; }
        public int      Retries  { get; set; }
    }
}
