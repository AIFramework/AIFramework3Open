using AI.LLM.Agents;
using AI.LLM.Agents.Tools;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Prompt-fallback существует для моделей, которые НЕ умеют нативные вызовы инструментов: вызов
/// разбирается из текста ответа. Значит и результат обязан вернуться текстом — ответ
/// <c>role=tool</c> ссылается на <c>tool_call_id</c> из ответа модели, а такого вызова у
/// провайдера не было, и переписку с ответом на несуществующий вызов он отвергает целиком.
/// </summary>
public class AgentPromptFallbackTests
{
    [Fact]
    public async Task Agent_RunAsync_WithPromptFallback_ReturnsToolResultsAsPlainMessage()
    {
        // Модель без нативного function calling отвечает JSON-блоком в тексте — так её просит
        // системный промпт, дополненный описанием инструментов.
        const string fenced = """
            ```json
            {"tool": "echo", "arguments": {"text": "мир"}}
            ```
            """;

        var llm = new FakeLLMClient()
            .EnqueueFull(FakeLLMClient.WithText(fenced))
            .EnqueueFull(FakeLLMClient.WithText("эхо получено"));

        Agent agent = AgentBuilder.Create()
            .WithLLM(llm)
            .WithTools(new SampleTools())
            .WithPromptFallback()
            .Build();

        AgentResult result = await agent.RunAsync("повтори «мир»");

        Assert.Equal("эхо получено", result.Answer);

        // Второе обращение — то, где результат инструмента возвращается модели.
        List<LLMMessage> second = llm.SentMessages[1];

        Assert.DoesNotContain(second, m => m.Role == LLMMessage.ToolRole);
        Assert.DoesNotContain(second, m => m.ToolCalls is { Count: > 0 });
        Assert.DoesNotContain(second, m => m.ToolCallId != null);

        LLMMessage last = second[^1];
        Assert.Equal(LLMMessage.UserRole, last.Role);
        Assert.Contains("эхо: мир", last.Content?.ToString());
        Assert.Contains("echo", last.Content?.ToString());
    }

    [Fact]
    public async Task Agent_RunAsync_WithPromptFallback_StillReportsToolCallsInSteps()
    {
        var llm = new FakeLLMClient()
            .EnqueueFull(FakeLLMClient.WithText("""{"tool": "echo", "arguments": {"text": "мир"}}"""))
            .EnqueueFull(FakeLLMClient.WithText("готово"));

        Agent agent = AgentBuilder.Create()
            .WithLLM(llm)
            .WithTools(new SampleTools())
            .WithPromptFallback()
            .Build();

        AgentResult result = await agent.RunAsync("повтори «мир»");

        // Наблюдаемость шага не пострадала: вызов и его результат видны потребителю,
        // даже если в переписку с моделью они ушли текстом.
        AgentStep step = result.Steps[0];
        Assert.Equal("echo", step.ToolCalls!.Single().Function.Name);
        Assert.Equal("эхо: мир", step.ToolResults!.Single().Content);
        Assert.Equal(1, result.Usage.ToolCalls);
    }

    [Fact]
    public async Task Agent_RunAsync_WithoutPromptFallback_KeepsNativeToolProtocol()
    {
        var llm = new FakeLLMClient()
            .EnqueueFull(FakeLLMClient.WithToolCalls(("call_1", "echo", """{"text":"мир"}""")))
            .EnqueueFull(FakeLLMClient.WithText("готово"));

        Agent agent = AgentBuilder.Create()
            .WithLLM(llm)
            .WithTools(new SampleTools())
            .Build();

        await agent.RunAsync("повтори «мир»");

        // Модель ВЫДАЛА вызов — значит ответ на него обязан быть role=tool с тем же id.
        List<LLMMessage> second = llm.SentMessages[1];
        LLMMessage toolResult = Assert.Single(second, m => m.Role == LLMMessage.ToolRole);
        Assert.Equal("call_1", toolResult.ToolCallId);
    }

    [Fact]
    public void ToolRegistry_ToPromptResultMessages_CarriesImagesInOneUserMessage()
    {
        var image = new AI.LLM.Agents.Multimodal.AgentImage([0x89, 0x50, 0x4E, 0x47], "image/png");
        List<ToolExecutionResult> results =
        [
            new("id-1", "screenshot", "снято", true, TimeSpan.Zero, [image]),
            new("id-2", "reader", "не открылось", false, TimeSpan.Zero),
        ];

        LLMMessage message = Assert.Single(ToolRegistry.ToPromptResultMessages(results));

        Assert.Equal(LLMMessage.UserRole, message.Role);

        var content = Assert.IsType<AI.LLM.Core.Models.Common.Messages.Content.MessageContent>(message.Content);
        Assert.Contains("снято", content.ToString());
        Assert.Contains("не открылось", content.ToString());

        // Отказ инструмента должен быть виден модели как отказ, а не как обычный результат.
        Assert.Contains("(ошибка)", content.ToString());
        Assert.Single(content.OfType<AI.LLM.Core.Models.Common.Messages.Content.ImageContent>());
    }

    private sealed class SampleTools
    {
        [AgentTool("echo", "Повторяет переданный текст")]
        public string Echo([ToolParameter("Текст для повтора")] string text) => "эхо: " + text;
    }
}
