using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Interop.Tests.Effects;

/// <summary>Инструмент со счетчиком вызовов: отдает итоги по очереди, последний повторяет.</summary>
internal sealed class CountingTool(ToolSpec spec, params Func<ToolCall, ToolContext, ToolResult?>[] outcomes) : ITool
{
    private int _calls;

    public ToolSpec Spec { get; } = spec;

    public int Calls => _calls;

    public List<ToolCall> Received { get; } = [];

    public List<ToolContext> Contexts { get; } = [];

    /// <summary>Бросить исключение вместо итога на этом вызове (с 1); 0 — никогда.</summary>
    public int ThrowOn { get; init; }

    public async IAsyncEnumerable<ToolUpdate> CallAsync(
        ToolCall call, ToolContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int number = Interlocked.Increment(ref _calls);
        lock (Received)
        {
            Received.Add(call);
            Contexts.Add(context);
        }

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        if (number == ThrowOn)
            throw new InvalidOperationException("обрыв связи");

        ToolResult? result = outcomes[Math.Min(number, outcomes.Length) - 1](call, context);
        if (result != null)
            yield return new ToolUpdate.Final { Result = result };
    }

    public static ToolResult Ok(string text = "сделано") =>
        new() { Status = ToolStatus.Ok, Text = text, Origin = new Origin { Kind = OriginKind.Trusted } };

    public static ToolResult Failed(string text = "сбой") =>
        new() { Status = ToolStatus.Failed, Text = text, Origin = new Origin { Kind = OriginKind.Trusted }, ErrorCode = "remote" };
}

/// <summary>Набор из списка инструментов: неизвестное имя — Failed.</summary>
internal sealed class ListToolSet(params ITool[] tools) : IToolSet
{
    public IReadOnlyList<ToolSpec> Available => [.. tools.Select(t => t.Spec)];

    public IAsyncEnumerable<ToolUpdate> CallAsync(ToolCall call, ToolContext context, CancellationToken cancellationToken = default)
    {
        ITool? tool = tools.FirstOrDefault(t => ToolSpec.NameComparer.Equals(t.Spec.Name, call.Name));
        return tool != null
            ? tool.CallAsync(call, context, cancellationToken)
            : Single(new ToolResult { Status = ToolStatus.Failed, Text = "нет", Origin = new Origin { Kind = OriginKind.Trusted } });
    }

    private static async IAsyncEnumerable<ToolUpdate> Single(ToolResult result)
    {
        await Task.Yield();
        yield return new ToolUpdate.Final { Result = result };
    }
}

/// <summary>Бюджет для тестов: предел шагов, срок и учет расхода.</summary>
internal sealed class TestBudget(int steps = int.MaxValue, long tokens = long.MaxValue, DateTimeOffset? deadline = null) : IRunBudget
{
    private int _steps;
    private long _tokens;

    public DateTimeOffset? Deadline { get; } = deadline;

    public int Taken => _steps;

    public long SpentTokens => _tokens;

    public bool TryStep(out StopCode stop)
    {
        if (_tokens >= tokens)
        {
            stop = StopCode.TokenLimit;
            return false;
        }

        if (_steps >= steps)
        {
            stop = StopCode.StepLimit;
            return false;
        }

        _steps++;
        stop = default;
        return true;
    }

    public void Spend(ModelUsage usage) => _tokens += usage.InputTokens + usage.OutputTokens;
}
