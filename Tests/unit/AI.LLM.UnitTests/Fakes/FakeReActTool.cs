using AI.LLM.Agents.ReAct.Tools;

namespace AI.LLM.UnitTests.Fakes;

/// <summary>Инструмент с заданным поведением, запоминающий все свои вызовы.</summary>
internal sealed class FakeReActTool : IReActTool
{
    private readonly Func<ReActToolInvocation, CancellationToken, Task<ReActToolOutcome>> _run;

    public FakeReActTool(
        string name,
        Func<ReActToolInvocation, CancellationToken, Task<ReActToolOutcome>>? run = null,
        IReadOnlyCollection<string>? tags = null)
    {
        Name = name;
        Tags = tags ?? [];
        _run = run ?? ((invocation, _) => Task.FromResult(ReActToolOutcome.Success("результат " + invocation.Arguments)));
    }

    public string Name { get; }

    public string Description => "тестовый инструмент";

    public string? ParametersJsonSchema => null;

    public IReadOnlyCollection<string> Tags { get; }

    /// <summary>Аргументы всех состоявшихся вызовов.</summary>
    public List<string> Invocations { get; } = [];

    public async IAsyncEnumerable<ReActToolEvent> ExecuteAsync(
        ReActToolInvocation invocation,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Invocations.Add(invocation.Arguments);
        yield return new ReActToolEvent.Progress("работаю", null);

        ReActToolOutcome outcome = await _run(invocation, cancellationToken);
        yield return new ReActToolEvent.Result(outcome);
    }
}
