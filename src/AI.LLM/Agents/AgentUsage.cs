using AI.LLM.Agents.Tools;
using AI.LLM.Core.Models.Common.Responses;

namespace AI.LLM.Agents;

/// <summary>
/// Полная статистика использования ресурсов агентом за один запуск:
/// LLM-токены (суммарно по всем итерациям) + вызовы инструментов.
/// </summary>
public sealed class AgentUsage
{
    /// <summary>Суммарное число токенов промпта (по всем итерациям).</summary>
    public int PromptTokens { get; private set; }

    /// <summary>Суммарное число токенов генерации.</summary>
    public int CompletionTokens { get; private set; }

    /// <summary>Суммарное число токенов reasoning.</summary>
    public int ReasoningTokens { get; private set; }

    /// <summary>Суммарное число токенов (prompt + completion).</summary>
    public int TotalTokens { get; private set; }

    /// <summary>Суммарная стоимость LLM-вызовов (если провайдер возвращает cost).</summary>
    public decimal? TotalCost { get; private set; }

    /// <summary>Количество LLM-вызовов (итераций ReAct).</summary>
    public int LlmCalls { get; private set; }

    /// <summary>Общее число вызовов инструментов.</summary>
    public int ToolCalls { get; private set; }

    /// <summary>Число успешных вызовов инструментов.</summary>
    public int ToolCallsSucceeded { get; private set; }

    /// <summary>Число неудачных вызовов инструментов.</summary>
    public int ToolCallsFailed { get; private set; }

    /// <summary>Суммарное время выполнения инструментов.</summary>
    public TimeSpan ToolsElapsed { get; private set; }

    /// <summary>Детализация по каждому инструменту.</summary>
    public IReadOnlyList<ToolUsageEntry> ToolDetails => _toolDetails;

    private readonly List<ToolUsageEntry> _toolDetails = [];
    private readonly Dictionary<string, int> _toolIndex = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Аккумулирует usage из одного LLM-ответа.</summary>
    internal void AddLlmUsage(Usage usage)
    {
        LlmCalls++;
        if (usage == null) return;

        PromptTokens += usage.PromptTokens;
        CompletionTokens += usage.CompletionTokens;
        ReasoningTokens += usage.ReasoningTokens;
        TotalTokens += usage.TotalTokens;

        var cost = CostExtractor.TryExtract(usage.Cost);
        if (cost.HasValue)
            TotalCost = (TotalCost ?? 0m) + cost.Value;
    }

    /// <summary>Аккумулирует результаты выполнения инструментов.</summary>
    internal void AddToolResults(IEnumerable<ToolExecutionResult> results)
    {
        if (results == null) return;

        foreach (var r in results)
        {
            ToolCalls++;
            if (r.IsSuccess) ToolCallsSucceeded++;
            else ToolCallsFailed++;
            ToolsElapsed += r.Elapsed;

            if (!_toolIndex.TryGetValue(r.ToolName, out var idx))
            {
                idx = _toolDetails.Count;
                _toolIndex[r.ToolName] = idx;
                _toolDetails.Add(new ToolUsageEntry(r.ToolName));
            }

            _toolDetails[idx].Add(r);
        }
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"  LLM-вызовы    {LlmCalls}");
        sb.AppendLine($"  Токены        {TotalTokens:N0}  (prompt {PromptTokens:N0} + completion {CompletionTokens:N0})");
        if (ReasoningTokens > 0)
            sb.AppendLine($"  Reasoning     {ReasoningTokens:N0}");
        if (TotalCost.HasValue)
            sb.AppendLine($"  Стоимость     ${TotalCost.Value:F6}");

        if (ToolCalls > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"  Инструменты   {ToolCalls}  ({ToolCallsSucceeded} ок / {ToolCallsFailed} ошибок)");
            sb.AppendLine($"  Время tools   {ToolsElapsed.TotalMilliseconds:F0} ms");

            foreach (var td in _toolDetails)
            {
                var avg = td.AverageElapsed.TotalMilliseconds;
                sb.AppendLine($"    · {td.ToolName}: {td.Calls}× -> avg {avg:F0} ms");
            }
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>Статистика использования одного инструмента.</summary>
public sealed class ToolUsageEntry
{
    /// <summary>Имя инструмента.</summary>
    public string ToolName { get; }

    /// <summary>Количество вызовов.</summary>
    public int Calls { get; private set; }

    /// <summary>Успешных вызовов.</summary>
    public int Succeeded { get; private set; }

    /// <summary>Неудачных вызовов.</summary>
    public int Failed { get; private set; }

    /// <summary>Суммарное время выполнения.</summary>
    public TimeSpan TotalElapsed { get; private set; }

    /// <summary>Среднее время выполнения.</summary>
    public TimeSpan AverageElapsed => Calls > 0 ? TotalElapsed / Calls : TimeSpan.Zero;

    internal ToolUsageEntry(string toolName) => ToolName = toolName;

    internal void Add(ToolExecutionResult r)
    {
        Calls++;
        if (r.IsSuccess) Succeeded++;
        else Failed++;
        TotalElapsed += r.Elapsed;
    }
}
