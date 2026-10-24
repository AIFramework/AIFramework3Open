using AI.LLM.Agents;
using AI.LLM.Agents.Orchestration;
using AI.LLM.Agents.Planning;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Оркестратор планов со своим исполнителем шага и утверждением плана: пользователь правит план до выполнения,
/// шаг без принятого результата повторяется с отклонённым ответом в запросе, исчерпанный — перепланируется
/// с новым утверждением; отказ останавливает задачу, независимые шаги яруса идут одновременно.
/// </summary>
public class PlanningAgentTests
{
    private const string TwoSteps = """
        {"steps":[
          {"id":"s1","description":"Прочитать Calc.cs","done_when":"в ответе назван метод Total"},
          {"id":"s2","description":"Написать тест","done_when":"в ответе есть тест","depends_on":["s1"]}
        ]}
        """;

    [Fact]
    public async Task PlanningAgent_RunAsync_ApprovedEditedPlanRetriesRejectedStepWithItsAnswer()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithText(TwoSteps));
        llm.EnqueueText("да").EnqueueText("нет").EnqueueText("да");
        var queries = new List<(string Step, string Query)>();
        var retries = new List<StepExecutionResult>();
        PlanningAgent planner = PlanningAgentBuilder.Create()
            .WithLLM(llm)
            .WithLlmStepValidator()
            .WithPlanApproval((plan, ct) => Task.FromResult(PlanGenerator.Arrange(plan.Goal,
                [plan.Steps[0], new PlanStep { Id = "s2", Description = "Написать тест xUnit", DoneWhen = "в ответе есть тест", DependsOn = ["s1"] }])))
            .WithStepExecutor((step, query, ct) => {
                queries.Add((step.Id, query));
                return Task.FromResult(Answer(step.Id == "s1" ? "Метод Total складывает числа." : "Тест: Assert.Equal(3, Total(1, 2))."));
            })
            .Build();
        planner.OnStepRetry += (sender, step) => retries.Add(step);

        PlanningAgentResult result = await planner.RunAsync("Покрой Calc тестом");

        Assert.True(result.Success);
        Assert.False(result.Rejected);
        Assert.Equal(["s1", "s2", "s2"], queries.Select(q => q.Step));
        Assert.Contains("Сделай этот шаг: Написать тест xUnit", queries[1].Query);
        Assert.Contains("Уже сделано:\n- Прочитать Calc.cs → Метод Total складывает числа.", queries[1].Query);
        Assert.Contains("Шаг примут, когда: в ответе есть тест", queries[1].Query);
        Assert.DoesNotContain("Прошлая попытка", queries[1].Query);
        Assert.Contains("Прошлая попытка не принята (критерий не выполнен). Её ответ: Тест: Assert.Equal", queries[2].Query);
        Assert.Equal("s2", Assert.Single(retries).Step.Id);
    }

    [Fact]
    public async Task PlanningAgent_RunAsync_RejectedPlanStopsBeforeAnyStep()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithText(TwoSteps));
        int executed = 0;
        PlanningAgent planner = PlanningAgentBuilder.Create()
            .WithLLM(llm)
            .WithPlanApproval((plan, ct) => Task.FromResult<PlanTree>(null))
            .WithStepExecutor((step, query, ct) => { executed++; return Task.FromResult(Answer("сделано")); })
            .Build();

        PlanningAgentResult result = await planner.RunAsync("Покрой Calc тестом");

        Assert.True(result.Rejected);
        Assert.False(result.Success);
        Assert.Equal(0, executed);
        Assert.Equal(2, result.FinalPlan.Steps.Count);
    }

    [Fact]
    public async Task PlanningAgent_RunAsync_ExhaustedStepIsReplannedAndNewPlanApprovedAgain()
    {
        var llm = new FakeLLMClient()
            .EnqueueFull(FakeLLMClient.WithText("""{"steps":[{"id":"a","description":"Первый способ","done_when":"готово"}]}"""))
            .EnqueueFull(FakeLLMClient.WithText("""{"steps":[{"id":"b","description":"Другой способ","done_when":"готово"}]}"""));
        llm.EnqueueText("нет").EnqueueText("да");
        var approved = new List<string>();
        PlanningAgent planner = PlanningAgentBuilder.Create()
            .WithLLM(llm)
            .WithLlmStepValidator()
            .WithMaxStepRetries(0)
            .WithPlanApproval((plan, ct) => { approved.Add(plan.Steps[0].Id); return Task.FromResult(plan); })
            .WithStepExecutor((step, query, ct) => Task.FromResult(Answer("ответ " + step.Id)))
            .Build();

        PlanningAgentResult result = await planner.RunAsync("Задача");

        Assert.True(result.Success);
        Assert.Equal(1, result.ReplanCount);
        Assert.Equal(["a", "b"], approved);
    }

    [Fact]
    public async Task PlanningAgent_RunAsync_IndependentStepsOfTierRunTogether()
    {
        var llm = new FakeLLMClient().EnqueueFull(FakeLLMClient.WithText("""
            {"steps":[{"id":"x","description":"Первый","done_when":"готово"},{"id":"y","description":"Второй","done_when":"готово"}]}
            """));
        var bothStarted = new TaskCompletionSource();
        int started = 0;
        PlanningAgent planner = PlanningAgentBuilder.Create()
            .WithLLM(llm)
            .WithParallelTiers()
            .WithStepExecutor(async (step, query, ct) => {
                if (Interlocked.Increment(ref started) == 2)
                    bothStarted.SetResult();
                // Второй шаг яруса должен начаться, пока первый ещё идёт.
                await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                return Answer("готово " + step.Id);
            })
            .Build();

        PlanningAgentResult result = await planner.RunAsync("Задача");

        Assert.True(result.Success);
        Assert.Equal(2, started);
    }

    private static AgentResult Answer(string text) => new(text, [], TimeSpan.Zero, new AgentUsage());
}
