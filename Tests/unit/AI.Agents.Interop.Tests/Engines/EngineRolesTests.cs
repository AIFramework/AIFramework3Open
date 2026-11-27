using System.Text.Json.Nodes;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Engines;
using AI.Agents.Interop.Models;
using AI.Agents.Interop.ReAct;
using AI.Agents.Interop.Tests.Effects;
using Xunit;

namespace AI.Agents.Interop.Tests.Engines;

/// <summary>
/// Роли вызовов движков: решение хода (шаг ReAct, план) спрашивает порт ролью decide, приемка шага — ролью judge.
/// На <see cref="ModelPort.Routed"/> с разными моделями на ролях приемка уходит на модель judge.
/// </summary>
public sealed class EngineRolesTests
{
    private static readonly ModelCapabilities Caps = new()
    {
        ToolCalling = true, Images = false, Audio = false, StructuredOutput = true, Streaming = false,
    };

    private static ScriptedClient Decider() => new ScriptedClient()
        .Then(ScriptedClient.Text("""{"steps":[{"id":"s1","description":"Узнать курс евро","done_when":"назван курс","depends_on":[]}]}"""))
        .Then(ScriptedClient.Text("Курс евро: 101,5"));

    private static EngineInput Input(ScriptedClient decide, ScriptedClient judge, JsonObject? options) => new()
    {
        RunId = "run-roles",
        Task = new EngineTask { Text = "Какой курс евро?" },
        Tools = new FreshSource("lookup").AsToolSet([Kit.Spec("lookup", ToolAction.Read) with { Idempotency = ToolIdempotency.Idempotent }]),
        Model = ModelPort.Routed(new Dictionary<ModelRole, IReadOnlyList<ModelSlot>>
        {
            [ModelRole.Decide] = [new ModelSlot("model-decide", ModelPort.Fixed(decide, Caps))],
            [ModelRole.Judge] = [new ModelSlot("model-judge", ModelPort.Fixed(judge, Caps))],
        }),
        Budget = new TestBudget(),
        Options = options,
    };

    [Fact]
    public async Task Plan_StepAcceptance_GoesToJudgeModel()
    {
        ScriptedClient decide = Decider();
        ScriptedClient judge = new ScriptedClient().Then(ScriptedClient.Text("ДА"));

        List<EngineEvent> events = await Kit.ReadAll(new PlanningEngineAdapter().RunAsync(
            Input(decide, judge, new JsonObject { ["acceptance"] = "model" })));

        EngineResult result = Assert.IsType<EngineEvent.Finished>(events[^1]).Result;
        Assert.Equal(EngineStatus.Done, result.Status);

        // План и шаг — модель решения хода; приемка шага — модель судьи, и только она.
        Assert.Equal(2, decide.Sent.Count);
        List<AI.LLM.Core.Models.Common.Messages.LLMMessage> asked = Assert.Single(judge.Sent);
        Assert.Contains("назван курс", asked[^1].Content!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_WithoutModelAcceptance_JudgeIsNotAsked()
    {
        ScriptedClient decide = Decider();
        var judge = new ScriptedClient();

        List<EngineEvent> events = await Kit.ReadAll(new PlanningEngineAdapter().RunAsync(Input(decide, judge, options: null)));

        Assert.Equal(EngineStatus.Done, Assert.IsType<EngineEvent.Finished>(events[^1]).Result.Status);
        Assert.Empty(judge.Sent);
    }

    [Fact]
    public async Task React_AcceptanceOption_IsRefused()
    {
        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(
            Input(Decider(), new ScriptedClient(), new JsonObject { ["acceptance"] = "model" })));

        EngineResult result = Assert.IsType<EngineEvent.Finished>(Assert.Single(events)).Result;
        Assert.Equal(EngineInteropCodes.InvalidOptions, result.StopCode.Name);
    }

    [Fact]
    public async Task Plan_UnknownAcceptanceValue_IsRefused()
    {
        List<EngineEvent> events = await Kit.ReadAll(new PlanningEngineAdapter().RunAsync(
            Input(Decider(), new ScriptedClient(), new JsonObject { ["acceptance"] = "maybe" })));

        Assert.Equal(EngineInteropCodes.InvalidOptions, Assert.IsType<EngineEvent.Finished>(Assert.Single(events)).Result.StopCode.Name);
    }
}
