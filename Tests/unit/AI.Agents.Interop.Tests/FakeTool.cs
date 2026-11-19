using System.Runtime.CompilerServices;
using System.Text.Json;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Инструмент контракта для тестов: отдает заданные кадры и запоминает, с чем его вызвали.
/// </summary>
internal sealed class FakeTool(ToolSpec spec, params ToolUpdate[] updates) : ITool
{
    /// <inheritdoc />
    public ToolSpec Spec { get; } = spec;

    /// <summary>Вид, который отвечает <see cref="Classify"/>; <see langword="null"/>: <c>Spec.Action</c>.</summary>
    public ToolAction? Classified { get; init; }

    /// <summary>Последний вызов.</summary>
    public ToolCall? LastCall { get; private set; }

    /// <summary>Контекст последнего вызова.</summary>
    public ToolContext? LastContext { get; private set; }

    /// <inheritdoc />
    public ToolAction Classify(ToolCall call) => Classified ?? Spec.Action;

    /// <inheritdoc />
    public async IAsyncEnumerable<ToolUpdate> CallAsync(
        ToolCall call, ToolContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastCall = call;
        LastContext = context;
        foreach (ToolUpdate update in updates)
        {
            await Task.Yield();
            yield return update;
        }
    }
}
