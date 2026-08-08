using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Policies;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.ToolCalling;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Настройки генерации задаются один раз при сборке и живут дольше отдельного запроса, а запрос
/// почти всегда что-то в них доопределяет. Здесь проверяется, что доопределяется копия: правка
/// общего экземпляра означала бы, что параллельный прогон уходит к модели с чужим списком
/// инструментов, — и заметить это по результату почти невозможно.
/// </summary>
public class SharedSettingsIsolationTests
{
    [Fact]
    public void GenerateSettings_Clone_CopiesEveryFieldAndDetachesLists()
    {
        var original = new GenerateSettings(temperature: 0.7, maxTokens: 555, streamId: "s-1")
        {
            ResponseFormat = ResponseFormat.CreateJsonObject(),
            Modalities = ["text"],
            Tools = [ToolDefinition.Create("a", "инструмент a", "{}")],
            IncludeUsage = true,
        };

        GenerateSettings copy = original.Clone();

        Assert.Equal(0.7, copy.Temperature);
        Assert.Equal(555, copy.MaxTokens);
        Assert.Equal("s-1", copy.StreamId);
        Assert.True(copy.IncludeUsage);
        Assert.Same(original.ResponseFormat, copy.ResponseFormat);

        // Списки отвязаны: их принято дополнять, и дополнение копии не должно доставаться оригиналу.
        copy.Tools!.Add(ToolDefinition.Create("b", "инструмент b", "{}"));
        copy.Modalities!.Add("image");
        Assert.Single(original.Tools!);
        Assert.Single(original.Modalities!);
    }

    [Fact]
    public void GenerateSettings_CloneWithStream_TurnsStreamingOnWithoutTouchingOriginal()
    {
        var original = new GenerateSettings(temperature: 0.3, maxTokens: 100);
        Assert.False(original.Stream);

        GenerateSettings streaming = original.CloneWithStream("s-42", "StreamMessage");

        Assert.True(streaming.Stream);
        Assert.Equal("s-42", streaming.StreamId);
        Assert.Equal(0.3, streaming.Temperature);

        Assert.False(original.Stream);
        Assert.Null(original.StreamId);
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_DoesNotWriteToolsIntoSharedSettings()
    {
        var shared = new GenerateSettings(temperature: 0.2, maxTokens: 300);
        var llm = new FakeLLMClient()
            .EnqueueFull(FakeLLMClient.WithText("готово"))
            .EnqueueFull(FakeLLMClient.WithText("готово"));

        var policy = new NativeToolCallPolicy(llm, shared);

        await policy.DecideAsync(Context([new FakeReActTool("web_search")]));
        await policy.DecideAsync(Context([new FakeReActTool("read_file")]));

        // Каждое обращение видит СВОЙ набор инструментов…
        Assert.Equal("web_search", llm.SentSettings[0]!.Tools.Single().Function.Name);
        Assert.Equal("read_file", llm.SentSettings[1]!.Tools.Single().Function.Name);

        // …а общий экземпляр остаётся таким, каким его дал вызывающий.
        Assert.Null(shared.Tools);
        Assert.Null(shared.ToolChoice);
    }

    [Fact]
    public async Task NativeToolCallPolicy_DecideAsync_KeepsToolsOfParallelRunsApart()
    {
        var shared = new GenerateSettings(temperature: 0.2, maxTokens: 300);
        var policy = new NativeToolCallPolicy(new FakeLLMClient(), shared);

        // Два прогона одного движка с разными наборами — то, ради чего инструменты и считаются
        // на каждый прогон.
        ReActPolicyContext first = Context([new FakeReActTool("web_search")]);
        ReActPolicyContext second = Context([new FakeReActTool("read_file"), new FakeReActTool("write_file")]);

        ReActDecision[] decisions = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(i => policy.DecideAsync(i % 2 == 0 ? first : second)));

        Assert.Equal(32, decisions.Length);
        Assert.Null(shared.Tools);
    }

    [Fact]
    public async Task StructuredJsonPolicy_DecideAsync_DoesNotWriteResponseFormatIntoSharedSettings()
    {
        var shared = new GenerateSettings(temperature: 0.0, maxTokens: 400);
        var llm = new FakeLLMClient().EnqueueText("""{"thought":"всё ясно","final":"ответ"}""");

        var policy = new StructuredJsonPolicy(llm, shared);

        await policy.DecideAsync(Context([new FakeReActTool("web_search")]));

        Assert.NotNull(llm.SentSettings[0]!.ResponseFormat);
        Assert.Null(shared.ResponseFormat);
    }

    private static ReActPolicyContext Context(IReadOnlyList<IReActTool> tools) =>
        new()
        {
            Query = new ReActQuery("вопрос"),
            Tools = tools,
            Trace = new ReActTrace(),
            SystemPrompt = "инструкция",
            StepNumber = 1,
            MaxSteps = 5,
        };
}
