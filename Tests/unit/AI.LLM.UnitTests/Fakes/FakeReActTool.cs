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

    private readonly object _sync = new();
    private readonly List<string> _invocations = [];

    /// <summary>Аргументы всех состоявшихся вызовов.</summary>
    public IReadOnlyList<string> Invocations
    {
        get
        {
            // Движок исполняет инструменты параллельно, так что запись и чтение
            // списка могут идти из разных потоков одновременно.
            lock (_sync)
                return _invocations.ToArray();
        }
    }

    public async IAsyncEnumerable<ReActToolEvent> ExecuteAsync(
        ReActToolInvocation invocation,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        lock (_sync)
            _invocations.Add(invocation.Arguments);
        yield return new ReActToolEvent.Progress("работаю", null);

        ReActToolOutcome outcome = await _run(invocation, cancellationToken);
        yield return new ReActToolEvent.Result(outcome);
    }
}
