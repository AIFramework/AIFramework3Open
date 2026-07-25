using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Policies;

namespace AI.LLM.UnitTests.Fakes;

/// <summary>
/// Заранее заданные решения шагов. Позволяет проверить весь цикл без обращения к модели
/// и посмотреть, что именно цикл показал модели на каждом шаге.
/// </summary>
internal sealed class FakeReActPolicy : IReActPolicy
{
    private readonly Queue<ReActDecision> _decisions;

    public FakeReActPolicy(params ReActDecision[] decisions) => _decisions = new Queue<ReActDecision>(decisions);

    /// <summary>Контексты всех состоявшихся обращений — в порядке вызовов.</summary>
    public List<ReActPolicyContext> Calls { get; } = [];

    /// <summary>Что вернуть, когда заготовленные решения кончились.</summary>
    public ReActDecision Fallback { get; set; } = ReActDecision.Final("готово");

    public Task<ReActDecision> DecideAsync(ReActPolicyContext context, CancellationToken cancellationToken = default)
    {
        Calls.Add(context);
        return Task.FromResult(_decisions.Count > 0 ? _decisions.Dequeue() : Fallback);
    }
}
