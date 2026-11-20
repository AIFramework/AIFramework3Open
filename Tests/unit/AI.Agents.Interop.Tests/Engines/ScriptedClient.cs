using System.Text.Json;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;

namespace AI.Agents.Interop.Tests.Engines;

/// <summary>
/// Клиент модели по сценарию: отдает ответы по очереди и запоминает сообщения каждого обращения. Ответ, которого
/// нет в очереди, — текст «готово».
/// </summary>
internal sealed class ScriptedClient : ILLMClient
{
    private readonly Queue<ChatCompletionsResponse> _responses = new();

    /// <summary>Сообщения каждого обращения в порядке вызовов.</summary>
    public List<List<LLMMessage>> Sent { get; } = [];

    /// <summary>Ответы, которые отдаются, когда очередь пуста.</summary>
    public Func<ChatCompletionsResponse> Fallback { get; set; } = () => Text("готово");

    public ScriptedClient Then(ChatCompletionsResponse response)
    {
        _responses.Enqueue(response);
        return this;
    }

    public static ChatCompletionsResponse Calls(params (string Id, string Name, string Arguments)[] calls) => new()
    {
        Choices =
        [
            new Choice
            {
                Message = new LLMMessage(LLMMessage.AssistantRole, string.Empty)
                {
                    ToolCalls = [.. calls.Select(c => new ToolCall { Id = c.Id, Function = new FunctionCall { Name = c.Name, Arguments = c.Arguments } })],
                },
            },
        ],
        Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 },
    };

    public static ChatCompletionsResponse Text(string text) => new()
    {
        Choices = [new Choice { Message = new LLMMessage(LLMMessage.AssistantRole, text) }],
        Usage = new Usage { PromptTokens = 7, CompletionTokens = 3, TotalTokens = 10 },
    };

    /// <summary>Сообщения обращений в виде, удобном для сравнения: роль, текст, вызовы и ответ на вызов.</summary>
    public List<string> Transcript() =>
        [.. Sent.Select(messages => string.Join("\n", messages.Select(Describe)))];

    private static string Describe(LLMMessage message)
    {
        string calls = message.ToolCalls is { Count: > 0 } toolCalls
            ? " calls=" + JsonSerializer.Serialize(toolCalls.Select(c => new { c.Id, c.Function?.Name, c.Function?.Arguments }))
            : string.Empty;
        return $"{message.Role}|{message.ToolCallId}|{message.Content}{calls}";
    }

    public Task<string> SendAsync(string text, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<string> SendAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ChatCompletionsResponse> SendFullAsync(
        IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null!, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (Sent)
            Sent.Add([.. messages]);
        return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : Fallback());
    }

    public Task<ChatCompletionsResponse> SendFullAsync(
        IEnumerable<LLMMessage> messages, GenerateSettings generateSettings, Action<LLMStreamChunk> onChunk,
        CancellationToken cancellationToken = default) =>
        SendFullAsync(messages, generateSettings, cancellationToken);

    public Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
