using System.Text;
using AI.LLM.Agents.Memory;
using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Память агента, где каждая ячейка — выполненный шаг плана.
/// При построении контекста форматирует историю шагов как преамбулу
/// к текущему запросу, давая агенту полный контекст о том что уже сделано.
/// <para>
/// <see cref="SaveInteractionAsync"/> намеренно не сохраняет сырой диалог —
/// ячейки добавляются явно через <see cref="AddCellAsync"/> из <see cref="PlanningAgent"/>.
/// </para>
/// </summary>
public sealed class StepMemory : IAgentMemory
{
    private readonly List<StepMemoryEntry> _cells = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Все ячейки памяти в порядке добавления.</summary>
    public IReadOnlyList<StepMemoryEntry> Cells
    {
        get
        {
            _lock.Wait();
            try { return [.. _cells]; }
            finally { _lock.Release(); }
        }
    }

    /// <summary>Добавляет ячейку завершённого шага.</summary>
    public async Task AddCellAsync(StepMemoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _lock.WaitAsync().ConfigureAwait(false);
        try { _cells.Add(entry); }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<List<LLMMessage>> BuildContextAsync(string query, string systemPrompt)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        List<StepMemoryEntry> snapshot;
        try { snapshot = [.. _cells]; }
        finally { _lock.Release(); }

        var messages = new List<LLMMessage>
        {
            LLMMessage.CreateMessage(Roles.System, systemPrompt)
        };

        var sb = new StringBuilder();

        if (snapshot.Count > 0)
        {
            sb.AppendLine("=== Completed steps ===");
            foreach (var cell in snapshot)
            {
                var status  = cell.Success ? "OK" : $"FAILED ({cell.Attempts} attempts)";
                var toolTag = cell.ToolName != null ? $" [{cell.ToolName}]" : "";
                var preview = cell.Result.Length > 200
                    ? cell.Result[..200] + "…"
                    : cell.Result;

                sb.AppendLine($"[{cell.StepId}]{toolTag} {cell.Description} → {status}: {preview}");
            }

            sb.AppendLine();
            sb.AppendLine("=== Current step ===");
        }

        sb.Append(query);
        messages.Add(LLMMessage.CreateMessage(Roles.User, sb.ToString()));
        return messages;
    }

    /// <summary>
    /// Не сохраняет сырой диалог — ячейки управляются через <see cref="AddCellAsync"/>.
    /// </summary>
    public Task SaveInteractionAsync(string query, string answer, List<LLMMessage> fullHistory)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task ClearAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try { _cells.Clear(); }
        finally { _lock.Release(); }
    }
}
