using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Engines;
using AI.Agents.Interop.Models;
using AI.Agents.Interop.ReAct;
using AI.Agents.Interop.Tests.Models;
using AI.LLM.Agents.ReAct.Tools;
using Xunit;

namespace AI.Agents.Interop.Tests.Effects;

/// <summary>
/// Вход гейта из описания инструмента: <c>source.AsToolSet(specs)</c> несет и вид действия, и повторяемость одной
/// записью, поэтому хосту не нужно собирать спецификацию второй раз. И итог движка при сбое модели.
/// </summary>
public sealed class ToolEffectInputTests
{
    private static ToolSpec Spec(string name, ToolAction action, ToolIdempotency? idempotency) =>
        Kit.Spec(name, action) with { Idempotency = idempotency };

    [Fact]
    public void AsToolSet_FromSpecs_CarriesActionAndIdempotency()
    {
        IToolSet set = new FreshSource("send", "lookup", "other").AsToolSet(
            [Spec("send", ToolAction.Irreversible, ToolIdempotency.WithKey), Spec("lookup", ToolAction.Read, null)]);

        ToolSpec send = set.Available.Single(s => s.Name == "send");
        ToolSpec lookup = set.Available.Single(s => s.Name == "lookup");
        ToolSpec other = set.Available.Single(s => s.Name == "other");

        Assert.Equal((ToolAction.Irreversible, ToolIdempotency.WithKey), (send.Action, send.Idempotency));

        // Не объявленная повторяемость и инструмент без описания — «неизвестно», а не «можно повторять».
        Assert.Equal((ToolAction.Read, ToolIdempotency.Unknown), (lookup.Action, lookup.Idempotency));
        Assert.Equal((ToolAction.Unknown, ToolIdempotency.Unknown), (other.Action, other.Idempotency));
    }

    [Fact]
    public void AsToolSet_OldEntry_StillWorks_WithoutIdempotency()
    {
        IToolSet set = new FreshSource("lookup").AsToolSet(actions: new Dictionary<string, ToolAction> { ["lookup"] = ToolAction.Read });

        ToolSpec lookup = Assert.Single(set.Available);
        Assert.Equal(ToolAction.Read, lookup.Action);
        Assert.Null(lookup.Idempotency);
    }

    /// <summary>Прежде сбой модели в движке кончался кодом engine_failure: отказ ключа выглядел поломкой движка.</summary>
    [Fact]
    public async Task ReactEngine_ModelFailure_HasOneCodeAndKeepsCause()
    {
        var port = new FakePort
        {
            Answer = new LlmAnswer { Status = LlmStatus.Failed, Failure = LlmFailure.Access, FailureText = "ключ отклонен" },
        };
        var input = new EngineInput
        {
            RunId = "run-model-failure",
            Task = new EngineTask { Text = "вопрос" },
            Tools = new FreshSource("lookup").AsToolSet([Spec("lookup", ToolAction.Read, ToolIdempotency.Idempotent)]),
            Model = port,
            Budget = new TestBudget(),
        };

        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(input));

        EngineResult result = Assert.IsType<EngineEvent.Finished>(events[^1]).Result;
        Assert.Equal(EngineStatus.Failed, result.Status);
        Assert.Equal(StopCode.ModelFailure, result.StopCode);
        string reason = Assert.Single(result.Unfinished!);
        Assert.StartsWith(nameof(LlmFailure.Access), reason, StringComparison.Ordinal);
        Assert.Contains("ключ отклонен", reason, StringComparison.Ordinal);
    }
}
