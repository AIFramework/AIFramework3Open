using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;

namespace AI.LLM.UnitTests.Fakes;

/// <summary>
/// Клиент модели с заранее заданными ответами. Запоминает, что именно ему отправили —
/// без этого не проверить, что переписка восстановлена по протоколу.
/// </summary>
internal sealed class FakeLLMClient : ILLMClient
{
    private readonly Queue<ChatCompletionsResponse> _full = new();
    private readonly Queue<string> _text = new();

    /// <summary>Сообщения каждого обращения — в порядке вызовов.</summary>
    public List<List<LLMMessage>> SentMessages { get; } = [];

    /// <summary>Настройки каждого обращения — в порядке вызовов.</summary>
    public List<GenerateSettings?> SentSettings { get; } = [];

    /// <summary>Промпты обращений без контекста — в порядке вызовов.</summary>
    public List<string> SentPrompts { get; } = [];

    /// <summary>
    /// Задержка перед выдачей ответа. Позволяет проверить, что происходит с памятью, пока
    /// обращение к модели ещё не завершилось.
    /// </summary>
    public Func<Task>? BeforeSend { get; set; }

    public FakeLLMClient EnqueueFull(ChatCompletionsResponse response)
    {
        _full.Enqueue(response);
        return this;
    }

    public FakeLLMClient EnqueueText(string text)
    {
        _text.Enqueue(text);
        return this;
    }

    /// <summary>Ответ с вызовами инструментов.</summary>
    public static ChatCompletionsResponse WithToolCalls(params (string Id, string Name, string Arguments)[] calls)
    {
        var message = new LLMMessage(LLMMessage.AssistantRole, string.Empty)
        {
            ToolCalls = calls
                .Select(c => new ToolCall { Id = c.Id, Function = new FunctionCall { Name = c.Name, Arguments = c.Arguments } })
                .ToList(),
        };

        return new ChatCompletionsResponse
        {
            Choices = [new Choice { Message = message }],
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 },
        };
    }

    /// <summary>Ответ без вызовов инструментов — обычный текст.</summary>
    public static ChatCompletionsResponse WithText(string text) =>
        new()
        {
            Choices = [new Choice { Message = new LLMMessage(LLMMessage.AssistantRole, text) }],
            Usage = new Usage { PromptTokens = 7, CompletionTokens = 3, TotalTokens = 10 },
        };

    public async Task<string> SendAsync(
        string text, GenerateSettings? generateSettings = null, CancellationToken cancellationToken = default)
    {
        SentPrompts.Add(text);
        SentSettings.Add(generateSettings);

        if (BeforeSend != null)
            await BeforeSend().ConfigureAwait(false);

        return _text.Count > 0 ? _text.Dequeue() : string.Empty;
    }

    public async Task<string> SendAsync(
        IEnumerable<LLMMessage> messages,
        GenerateSettings? generateSettings = null,
        CancellationToken cancellationToken = default)
    {
        SentMessages.Add(messages.ToList());
        SentSettings.Add(generateSettings);

        if (BeforeSend != null)
            await BeforeSend().ConfigureAwait(false);

        return _text.Count > 0 ? _text.Dequeue() : string.Empty;
    }

    public Task<ChatCompletionsResponse> SendFullAsync(
        IEnumerable<LLMMessage> messages,
        GenerateSettings? generateSettings = null,
        CancellationToken cancellationToken = default)
    {
        SentMessages.Add(messages.ToList());
        SentSettings.Add(generateSettings);
        return Task.FromResult(_full.Count > 0 ? _full.Dequeue() : WithText(string.Empty));
    }

    public Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
