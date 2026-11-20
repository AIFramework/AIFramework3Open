using AI.Agents.Contracts.Runs;
using System.Text.Json.Nodes;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;
using AI.Agents.Interop.Engines;
using AI.Agents.Interop.Models;
using AI.Agents.Interop.ReAct;
using AI.Agents.Interop.Tests.Effects;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Xunit;

namespace AI.Agents.Interop.Tests.Engines;

/// <summary>
/// Адаптеры IEngine над ReActEngine («react») и PlanningAgent («plan»): эталон против старого входа, перевод итога,
/// честные границы (продолжение, параметры, бюджет).
/// </summary>
public sealed class EngineAdapterTests
{
    private static readonly ModelCapabilities ToolCalling = new()
    {
        ToolCalling = true, Images = false, Audio = false, StructuredOutput = true, Streaming = false,
    };

    /// <summary>Сценарий: модель зовет lookup, потом отвечает текстом.</summary>
    private static ScriptedClient Scenario() => new ScriptedClient()
        .Then(ScriptedClient.Calls(("call_1", "lookup", """{"q":"курс евро"}""")))
        .Then(ScriptedClient.Text("Курс евро: 101,5"));

    private static IReActTool Lookup(List<string>? seen = null) => DelegateReActTool.FromOutcome(
        "lookup",
        "Найти справку",
        (invocation, _) =>
        {
            seen?.Add(invocation.Arguments);
            return Task.FromResult(ReActToolOutcome.Success("найдено: " + invocation.Arguments));
        });

    private static EngineInput Input(ScriptedClient client, IToolSet tools, IRunBudget? budget = null, JsonObject? options = null,
        JsonObject? state = null, EngineContext? context = null) => new()
    {
        RunId = "run-1",
        Task = new EngineTask { Text = "Какой курс евро?" },
        Tools = tools,
        Model = ModelPort.Fixed(client, ToolCalling),
        Budget = budget ?? new TestBudget(),
        Options = options,
        State = state,
        Context = context,
    };

    private static IToolSet Tools(IReActTool tool, ToolAction action = ToolAction.Read) =>
        new Source(tool).AsToolSet(actions: new Dictionary<string, ToolAction> { [tool.Name] = action });

    /// <summary>
    /// Эталон: один сценарий через старый вход (ReActAgentBuilder + ILLMClient + IReActTool) и через адаптер
    /// (IModelPort, IToolSet, гейт) дает тот же ответ, те же сообщения модели и тот же аргумент инструменту.
    /// </summary>
    [Fact]
    public async Task React_Reference_SameAnswerAndSameModelMessages()
    {
        ScriptedClient oldClient = Scenario();
        var oldSeen = new List<string>();
        ReActResult old = await ReActAgentBuilder.Create()
            .WithNativeToolCalling(oldClient)
            .WithSystemPrompt("Ты справочник.")
            .WithTool(Lookup(oldSeen))
            .Build()
            .RunAsync("Какой курс евро?");

        ScriptedClient newClient = Scenario();
        var newSeen = new List<string>();
        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(
            Input(newClient, Tools(Lookup(newSeen)), context: new EngineContext { Instructions = "Ты справочник." })));

        EngineResult result = Assert.IsType<EngineEvent.Finished>(events[^1]).Result;
        Assert.Equal(EngineStatus.Done, result.Status);
        Assert.Equal(StopCode.FinalAnswer, result.StopCode);
        Assert.Equal(old.Answer, result.Text);
        Assert.Equal(oldSeen, newSeen);
        Assert.Equal(2, oldClient.Sent.Count);
        Assert.Equal(oldClient.Transcript(), newClient.Transcript());
        Assert.Contains("Ты справочник.", newClient.Transcript()[0], StringComparison.Ordinal);
        Assert.Contains("найдено:", newClient.Transcript()[1], StringComparison.Ordinal);
        Assert.Contains("call_1", newClient.Transcript()[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task React_Events_EndWithOneFinished_AndToolFinishedCarriesKey()
    {
        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(Input(Scenario(), Tools(Lookup()))));

        Assert.Single(events.OfType<EngineEvent.Finished>());
        Assert.IsType<EngineEvent.Finished>(events[^1]);
        Assert.Contains(events, e => e is EngineEvent.Step { Path.Tier: 1 });
        Assert.Contains(events, e => e is EngineEvent.ToolStarted { Call.Name: "lookup" });

        EngineEvent.ToolFinished finished = Assert.Single(events.OfType<EngineEvent.ToolFinished>());
        Assert.Equal(ToolStatus.Ok, finished.Result.Status);
        Assert.Equal(EffectKeys.Create("run-1", "lookup", Kit.Json("""{"q":"курс евро"}""")), finished.Call.EffectKey);

        Assert.Equal(2, events.OfType<EngineEvent.Spent>().Count());
        EngineResult result = ((EngineEvent.Finished)events[^1]).Result;
        Assert.Equal(17, result.Usage!.InputTokens);
        Assert.Equal(8, result.Usage.OutputTokens);
    }

    [Fact]
    public async Task React_IterationLimit_IsIncomplete_NotDone()
    {
        var client = new ScriptedClient { Fallback = () => ScriptedClient.Calls(("c" + Guid.NewGuid().ToString("N"), "lookup", "{\"q\":\"" + Guid.NewGuid() + "\"}")) };

        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(
            Input(client, Tools(Lookup()), options: new JsonObject { ["maxIterations"] = 2 })));

        EngineResult result = ((EngineEvent.Finished)events[^1]).Result;
        Assert.Equal(EngineStatus.Incomplete, result.Status);
        Assert.Equal(StopCode.StepLimit, result.StopCode);
        Assert.NotNull(result.Unfinished);
    }

    [Fact]
    public async Task React_State_IsRefusedExplicitly_WithoutModelCalls()
    {
        ScriptedClient client = Scenario();

        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(
            Input(client, Tools(Lookup()), state: new JsonObject { ["step"] = 2 })));

        EngineResult result = Assert.IsType<EngineEvent.Finished>(Assert.Single(events)).Result;
        Assert.Equal(EngineStatus.Failed, result.Status);
        Assert.Equal(EngineInteropCodes.ContinuationNotSupported, result.StopCode.Name);
        Assert.Empty(client.Sent);
    }

    [Theory]
    [InlineData("""{"temperature":0.2}""")]
    [InlineData("""{"maxIterations":0}""")]
    [InlineData("""{"decisions":"magic"}""")]
    public async Task React_BadOptions_AreRefused(string options)
    {
        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(
            Input(Scenario(), Tools(Lookup()), options: (JsonObject)JsonNode.Parse(options)!)));

        EngineResult result = Assert.IsType<EngineEvent.Finished>(Assert.Single(events)).Result;
        Assert.Equal(EngineInteropCodes.InvalidOptions, result.StopCode.Name);
    }

    [Fact]
    public async Task React_BudgetSteps_StopBeforeNextStep()
    {
        ScriptedClient client = Scenario();
        var budget = new TestBudget(steps: 1);

        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(Input(client, Tools(Lookup()), budget)));

        EngineResult result = ((EngineEvent.Finished)events[^1]).Result;
        Assert.Equal(EngineStatus.Incomplete, result.Status);
        Assert.Equal(StopCode.StepLimit, result.StopCode);
        Assert.Single(client.Sent);
    }

    [Fact]
    public async Task React_BudgetTokens_AreSpent_AndCheckedAtNextStep()
    {
        ScriptedClient client = Scenario();
        var budget = new TestBudget(tokens: 10);

        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(Input(client, Tools(Lookup()), budget)));

        EngineResult result = ((EngineEvent.Finished)events[^1]).Result;
        Assert.Equal(15, budget.SpentTokens);
        Assert.Equal(StopCode.TokenLimit, result.StopCode);
        Assert.Equal(EngineStatus.Incomplete, result.Status);
    }

    [Fact]
    public async Task React_ExpiredDeadline_IsIncompleteTimeLimit()
    {
        ScriptedClient client = Scenario();
        List<EngineEvent> events = await Kit.ReadAll(new ReActEngineAdapter().RunAsync(
            Input(client, Tools(Lookup()), new TestBudget(deadline: DateTimeOffset.UtcNow.AddMinutes(-1)))));

        EngineResult result = Assert.IsType<EngineEvent.Finished>(Assert.Single(events)).Result;
        Assert.Equal(StopCode.TimeLimit, result.StopCode);
        Assert.Empty(client.Sent);
    }

    [Fact]
    public async Task React_HostGate_IsReused_AndJournalIsFilled()
    {
        var mail = new CountingTool(
            Kit.Spec("lookup", ToolAction.Irreversible) with { Idempotency = ToolIdempotency.NotIdempotent },
            (_, _) => CountingTool.Ok("отправлено"));
        var gate = new EffectGate(new ListToolSet(mail));

        await Kit.ReadAll(new ReActEngineAdapter().RunAsync(Input(Scenario(), gate)));

        EffectRecord record = Assert.Single(gate.Journal);
        Assert.Equal("lookup", record.Tool);
        Assert.Equal(ToolStatus.Ok, record.Result.Status);
        Assert.Equal("run-1", mail.Contexts.Single().RunId);
    }

    [Fact]
    public async Task Plan_RunsStepsThroughGate_AndFinishesDone()
    {
        var client = new ScriptedClient()
            .Then(ScriptedClient.Text("""{"steps":[{"id":"s1","description":"Узнать курс евро","depends_on":[]}]}"""))
            .Then(ScriptedClient.Calls(("call_1", "lookup", """{"q":"курс евро"}""")))
            .Then(ScriptedClient.Text("Курс евро: 101,5"));
        var mail = new CountingTool(
            Kit.Spec("lookup", ToolAction.Irreversible) with { Idempotency = ToolIdempotency.NotIdempotent },
            (_, _) => CountingTool.Ok("найдено"));
        var gate = new EffectGate(new ListToolSet(mail));

        var adapter = new PlanningEngineAdapter();
        List<EngineEvent> events = await Kit.ReadAll(adapter.RunAsync(Input(client, gate)));

        Assert.Equal("plan", adapter.Key);
        EngineResult result = Assert.IsType<EngineEvent.Finished>(events[^1]).Result;
        Assert.Single(events.OfType<EngineEvent.Finished>());
        Assert.Equal(EngineStatus.Done, result.Status);
        Assert.Equal("Курс евро: 101,5", result.Text);
        Assert.Equal(1, mail.Calls);
        Assert.Single(gate.Journal);
        Assert.Contains(events, e => e is EngineEvent.Step { Path.Branch: "s1" });
        Assert.Contains(events, e => e is EngineEvent.ToolFinished { Path.Branch: "s1" });
    }

    /// <summary>
    /// Предел итераций попытки шага — 30, как у исполнителя шагов старого PlanningAgent; maxIterations его заменяет.
    /// Попытка шага — обращения к модели от запроса шага до следующего запроса шага: у каждого следующего
    /// обращения той же попытки сообщений больше.
    /// </summary>
    [Theory]
    [InlineData(null, 30)]
    [InlineData(3, 3)]
    public async Task Plan_StepIterations_MatchOldExecutor(int? maxIterations, int expected)
    {
        int n = 0;
        var client = new ScriptedClient
        {
            Fallback = () =>
            {
                n++;
                return ScriptedClient.Calls(("c" + n, "lookup", "{\"q\":\"" + n + "\"}"));
            },
        }.Then(ScriptedClient.Text("""{"steps":[{"id":"s1","description":"Узнать курс евро","depends_on":[]}]}"""));

        JsonObject? options = maxIterations is int count ? new JsonObject { ["maxIterations"] = count } : null;
        await Kit.ReadAll(new PlanningEngineAdapter().RunAsync(Input(client, Tools(Lookup()), options: options)));

        // Обращение 0 — планировщик; дальше первая попытка шага, пока число сообщений растет.
        int attempt = 1;
        while (attempt + 1 < client.Sent.Count && client.Sent[attempt + 1].Count > client.Sent[attempt].Count)
            attempt++;

        Assert.Equal(expected, attempt);
    }

    /// <summary>
    /// Заявленное совпадение со старым исполнителем держится, пока старый PlanningAgentBuilder задает тот же предел:
    /// изменение в AI.LLM ломает этот тест, и адаптер пересматривают.
    /// </summary>
    [Fact]
    public void Plan_StepIterations_AreThoseOfOldBuilder()
    {
        string builder = File.ReadAllText(Path.Combine(Repository.Root, "src/AI.LLM/Agents/Orchestration/PlanningAgentBuilder.cs"));
        string adapter = File.ReadAllText(Path.Combine(Repository.Root, "src/AI.Agents.Interop/Engines/PlanningEngineAdapter.cs"));

        Assert.Contains(".WithMaxIterations(30)", builder, StringComparison.Ordinal);
        Assert.Contains("private const int StepMaxIterations = 30;", adapter, StringComparison.Ordinal);

        // Температура старого исполнителя в порт не выражается и записана расхождением.
        Assert.Contains(".WithTemperature(0.15)", builder, StringComparison.Ordinal);
        string readme = File.ReadAllText(Path.Combine(Repository.Root, "src/AI.Agents.Interop/README.md"));
        Assert.Contains("0,15", readme, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_State_IsRefusedExplicitly()
    {
        List<EngineEvent> events = await Kit.ReadAll(new PlanningEngineAdapter().RunAsync(
            Input(Scenario(), Tools(Lookup()), state: new JsonObject())));

        Assert.Equal(EngineInteropCodes.ContinuationNotSupported,
            Assert.IsType<EngineEvent.Finished>(Assert.Single(events)).Result.StopCode.Name);
    }

    [Fact]
    public void Keys_AreReactAndPlan()
    {
        Assert.Equal("react", new ReActEngineAdapter().Key);
        Assert.Equal("plan", new PlanningEngineAdapter().Key);
    }

    private sealed class Source(params IReActTool[] tools) : IReActToolSource
    {
        public IEnumerable<IReActTool> GetTools(ReActRunContext context) => tools;
    }
}
