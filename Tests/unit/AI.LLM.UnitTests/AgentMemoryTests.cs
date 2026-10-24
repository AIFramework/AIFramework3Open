using AI.LLM.Agents.Memory;
using AI.LLM.Agents.ReAct;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Память агента. Здесь проверяется то, что не видно по результату одного прогона: сжатие должно
/// СОКРАЩАТЬ контекст, а композит — действительно доносить долгосрочную память до промпта.
/// И то и другое ломается молча, без единой ошибки.
/// </summary>
public class AgentMemoryTests
{
    [Fact]
    public async Task SummarizationMemory_SaveInteractionAsync_ReplacesSummaryInsteadOfAppending()
    {
        var llm = new FakeLLMClient()
            .EnqueueText("сводка №1")
            .EnqueueText("сводка №2");

        var memory = new SummarizationMemory(llm, maxMessages: 4);

        // Порог в 4 сообщения превышается каждой третьей репликой: шесть реплик — два сжатия.
        // Второе должно ЗАМЕНИТЬ первую сводку, а не приписаться к ней.
        for (int i = 0; i < 6; i++)
            await memory.SaveInteractionAsync($"вопрос {i}", $"ответ {i}", null);

        Assert.Equal(2, llm.SentPrompts.Count);

        List<LLMMessage> context = await memory.BuildContextAsync("что дальше", "ты ассистент");
        string system = context[0].Content!.ToString()!;

        Assert.Contains("сводка №2", system);
        Assert.DoesNotContain("сводка №1", system);
    }

    [Fact]
    public async Task SummarizationMemory_SaveInteractionAsync_FeedsPreviousSummaryIntoNextRequest()
    {
        var llm = new FakeLLMClient()
            .EnqueueText("первая сводка")
            .EnqueueText("вторая сводка");

        var memory = new SummarizationMemory(llm, maxMessages: 4);

        for (int i = 0; i < 6; i++)
            await memory.SaveInteractionAsync($"вопрос {i}", $"ответ {i}", null);

        // Замещение не должно означать потерю: прежняя сводка обязана уйти в следующий запрос.
        Assert.Contains("первая сводка", llm.SentPrompts[1]);
    }

    [Fact]
    public async Task SummarizationMemory_SaveInteractionAsync_KeepsContextShorterThanThreshold()
    {
        var llm = new FakeLLMClient();
        for (int i = 0; i < 20; i++)
            llm.EnqueueText("сводка");

        var memory = new SummarizationMemory(llm, maxMessages: 4);

        for (int i = 0; i < 30; i++)
            await memory.SaveInteractionAsync($"вопрос {i}", $"ответ {i}", null);

        List<LLMMessage> context = await memory.BuildContextAsync("что дальше", "ты ассистент");

        // Ради этого сжатие и существует: контекст не растёт с числом реплик.
        Assert.True(context.Count <= 6, $"контекст разросся до {context.Count} сообщений");
    }

    [Fact]
    public async Task SummarizationMemory_ClearAsync_DiscardsSummaryFromCompactionAlreadyInFlight()
    {
        var llm = new FakeLLMClient().EnqueueText("сводка устаревшего диалога");
        var memory = new SummarizationMemory(llm, maxMessages: 4);

        // Две реплики — история ровно на пороге, сжатие ещё не запускается.
        await memory.SaveInteractionAsync("вопрос 0", "ответ 0", null);
        await memory.SaveInteractionAsync("вопрос 1", "ответ 1", null);
        Assert.Empty(llm.SentPrompts);

        // Дальше сжатие запустится и остановится в ожидании модели.
        var gate = new TaskCompletionSource();
        llm.BeforeSend = () => gate.Task;

        Task compaction = memory.SaveInteractionAsync("последний", "ответ", null);
        Assert.False(compaction.IsCompleted);

        // Память очищена, пока сжатие ждёт модель.
        await memory.ClearAsync();
        gate.SetResult();
        await compaction;

        List<LLMMessage> context = await memory.BuildContextAsync("новый разговор", "ты ассистент");

        Assert.DoesNotContain("сводка устаревшего диалога", context[0].Content!.ToString());
        Assert.Equal(2, context.Count); // только system + текущий запрос
    }

    [Fact]
    public async Task CompositeMemory_BuildContextAsync_TakesRecallFromLongTermMemory()
    {
        var longTerm = new FakeRecallMemory("пользователь любит краткие ответы");
        var composite = new CompositeMemory(new SlidingWindowMemory(), longTerm);

        List<LLMMessage> context = await composite.BuildContextAsync("как дела", "ты ассистент");

        string system = context[0].Content!.ToString()!;
        Assert.Contains("ты ассистент", system);
        Assert.Contains("пользователь любит краткие ответы", system);
        Assert.Equal("как дела", longTerm.RecalledFor);
    }

    [Fact]
    public async Task CompositeMemory_BuildContextAsync_KeepsPromptCleanWhenNothingRecalled()
    {
        var composite = new CompositeMemory(new SlidingWindowMemory(), new FakeRecallMemory(null));

        List<LLMMessage> context = await composite.BuildContextAsync("как дела", "ты ассистент");

        Assert.Equal("ты ассистент", context[0].Content!.ToString());
    }

    [Fact]
    public async Task CompositeMemory_BuildContextAsync_KeepsShortTermHistory()
    {
        var shortTerm = new SlidingWindowMemory();
        var composite = new CompositeMemory(shortTerm, new FakeRecallMemory("вспомнилось"));

        await shortTerm.SaveInteractionAsync("первый вопрос", "первый ответ", null);

        List<LLMMessage> context = await composite.BuildContextAsync("второй вопрос", "ты ассистент");

        Assert.Contains(context, m => m.Content?.ToString() == "первый вопрос");
        Assert.Contains(context, m => m.Content?.ToString() == "первый ответ");
        Assert.Equal("второй вопрос", context[^1].Content?.ToString());
    }

    [Fact]
    public async Task CompositeMemory_BuildContextAsync_FallsBackToPromptDeltaForMemoryWithoutRecall()
    {
        // Долгосрочная память без IRecallMemory, но дописывающая свой блок в системный промпт —
        // запасной путь обязан её вклад сохранить.
        var composite = new CompositeMemory(new SlidingWindowMemory(), new PromptAugmentingMemory());

        List<LLMMessage> context = await composite.BuildContextAsync("как дела", "ты ассистент");

        Assert.Contains("дописано долгосрочной памятью", context[0].Content!.ToString());
    }

    /// <summary>Долгосрочная память, отдающая воспоминания по контракту.</summary>
    private sealed class FakeRecallMemory(string recall) : IAgentMemory, IRecallMemory
    {
        public string? RecalledFor { get; private set; }

        public Task<string> RecallAsync(string query)
        {
            RecalledFor = query;
            return Task.FromResult(recall);
        }

        public Task<List<LLMMessage>> BuildContextAsync(string query, string systemPrompt) =>
            Task.FromResult(new List<LLMMessage> { LLMMessage.CreateMessage(Roles.System, systemPrompt) });

        public Task SaveInteractionAsync(string query, string answer, List<LLMMessage> fullHistory) =>
            Task.CompletedTask;

        public Task ClearAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task SummarizationMemory_SaveInteractionAsync_KeepsLastExchangeVerbatim()
    {
        var llm = new FakeLLMClient().EnqueueText("сводка");
        var memory = new SummarizationMemory(llm, maxMessages: 4);

        for (int i = 0; i < 3; i++)
            await memory.SaveInteractionAsync($"вопрос {i}", $"ответ {i}", null);

        List<LLMMessage> context = await memory.BuildContextAsync("что дальше", "ты ассистент");

        // Следующий вопрос обычно о последнем ответе — он не должен уйти в сводку.
        Assert.Contains("сводка", context[0].Content!.ToString());
        Assert.Equal(["вопрос 2", "ответ 2", "что дальше"], context.Skip(1).Select(m => m.Content!.ToString()));
        Assert.DoesNotContain("ответ 2", llm.SentPrompts[0]);
    }

    [Fact]
    public async Task SummarizationMemory_SaveInteractionAsync_ReportsSpendingOfCompaction()
    {
        var llm = new FakeLLMClient().EnqueueText("сводка");
        var memory = new SummarizationMemory(llm, maxMessages: 4);
        var spent = new List<AI.LLM.Core.Models.Common.Responses.Usage>();
        memory.Spent += spent.Add;

        for (int i = 0; i < 3; i++)
            await memory.SaveInteractionAsync($"вопрос {i}", $"ответ {i}", null);

        // Сжатие — отдельный оплачиваемый вызов модели: без события он выпадал из суммы диалога.
        Assert.Equal(7, Assert.Single(spent).PromptTokens);
    }

    [Fact]
    public async Task ReActEngine_WithMemory_PassesMemoryAdditionToSystemPrompt()
    {
        var policy = new FakeReActPolicy(ReActDecision.Final("готово"));
        ReActEngine engine = ReActAgentBuilder.Create()
            .WithPolicy(policy)
            .WithTool(new FakeReActTool("search"))
            .WithSystemPrompt("ты ассистент")
            .WithMemory(new PromptAugmentingMemory())
            .Build();

        await engine.RunAsync("вопрос");

        // Сводка и воспоминания живут в системном сообщении памяти: потерять его — потерять их.
        string prompt = policy.Calls[0].SystemPrompt;
        Assert.StartsWith("ты ассистент", prompt);
        Assert.EndsWith("дописано долгосрочной памятью", prompt);
        Assert.Equal(1, CountOf(prompt, "ты ассистент"));
        Assert.Empty(policy.Calls[0].Query.History);
    }

    private static int CountOf(string text, string part) =>
        (text.Length - text.Replace(part, string.Empty).Length) / part.Length;

    /// <summary>Долгосрочная память старого образца: вклад виден только в системном промпте.</summary>
    private sealed class PromptAugmentingMemory : IAgentMemory
    {
        public Task<List<LLMMessage>> BuildContextAsync(string query, string systemPrompt) =>
            Task.FromResult(new List<LLMMessage>
            {
                LLMMessage.CreateMessage(Roles.System, systemPrompt + "\n\nдописано долгосрочной памятью"),
                LLMMessage.CreateMessage(Roles.User, query),
            });

        public Task SaveInteractionAsync(string query, string answer, List<LLMMessage> fullHistory) =>
            Task.CompletedTask;

        public Task ClearAsync() => Task.CompletedTask;
    }
}
