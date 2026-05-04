using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.ToolCalling;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SKImageContent = Microsoft.SemanticKernel.ImageContent;

namespace AI.LLM.Integration.SemanticKernel.Adapters;

/// <summary>
/// Двунаправленное преобразование между <see cref="LLMMessage"/> и SK <see cref="ChatMessageContent"/>.
/// Поддерживает текст, изображения, tool_calls и tool-результаты.
/// </summary>
public static class MessageAdapter
{
    public static List<LLMMessage> FromChatHistory(ChatHistory chatHistory)
    {
        if (chatHistory == null)
            throw new ArgumentNullException(nameof(chatHistory));

        var messages = new List<LLMMessage>(chatHistory.Count);
        foreach (var skMessage in chatHistory)
            messages.Add(FromSKMessage(skMessage));
        return messages;
    }

    public static LLMMessage FromSKMessage(ChatMessageContent skMessage)
    {
        string role = skMessage.Role.Label;

        // Function call results (role=tool)
        var functionResults = skMessage.Items?.OfType<FunctionResultContent>().ToList();
        if (functionResults != null && functionResults.Count > 0)
        {
            var fr = functionResults[0];
            return new LLMMessage
            {
                Role = LLMMessage.ToolRole,
                Content = fr.Result?.ToString() ?? "",
                ToolCallId = fr.CallId,
                Name = fr.FunctionName,
            };
        }

        // Function call requests from assistant
        var functionCalls = skMessage.Items?.OfType<FunctionCallContent>().ToList();
        if (functionCalls != null && functionCalls.Count > 0)
        {
            var msg = new LLMMessage
            {
                Role = "assistant",
                Content = skMessage.Content,
                ToolCalls = functionCalls.Select(fc => new ToolCall
                {
                    Id = fc.Id,
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = fc.FunctionName,
                        Arguments = fc.Arguments != null
                            ? System.Text.Json.JsonSerializer.Serialize(fc.Arguments)
                            : "{}",
                    }
                }).ToList(),
            };
            return msg;
        }

        // Images
        var imageItems = skMessage.Items?.OfType<SKImageContent>().ToList();
        if (imageItems != null && imageItems.Count > 0)
        {
            var textParts = skMessage.Items?.OfType<TextContent>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t));
            string fullText = textParts != null && textParts.Any()
                ? string.Join("", textParts)
                : (skMessage.Content ?? string.Empty);

            var mc = new MessageContent(fullText);
            foreach (var img in imageItems)
            {
                if (img.Uri != null)
                    mc.AddImage(img.Uri.ToString());
            }
            return new LLMMessage(role, mc);
        }

        return new LLMMessage(role, skMessage.Content ?? string.Empty);
    }

    public static ChatMessageContent ToSKMessage(LLMMessage message, string modelId = null)
    {
        var role = ToAuthorRole(message.Role);
        string textContent = message.Content?.ToString() ?? string.Empty;

        var items = new ChatMessageContentItemCollection();

        // Tool call results
        if (message.Role == LLMMessage.ToolRole && !string.IsNullOrEmpty(message.ToolCallId))
        {
            items.Add(new FunctionResultContent(
                functionName: message.Name ?? "",
                callId: message.ToolCallId,
                result: textContent));
        }
        else
        {
            if (!string.IsNullOrEmpty(textContent))
                items.Add(new TextContent(textContent));

            // Tool calls from assistant
            if (message.ToolCalls != null)
            {
                foreach (var tc in message.ToolCalls)
                {
                    IDictionary<string, object> args = null;
                    if (!string.IsNullOrEmpty(tc.Function?.Arguments))
                    {
                        try
                        {
                            args = System.Text.Json.JsonSerializer
                                .Deserialize<Dictionary<string, object>>(tc.Function.Arguments);
                        }
                        catch { }
                    }

                    items.Add(new FunctionCallContent(
                        functionName: tc.Function?.Name ?? "",
                        id: tc.Id,
                        arguments: args is Dictionary<string, object> dict
                            ? new KernelArguments(dict)
                            : null));
                }
            }
        }

        // Images
        if (message.Images != null)
        {
            foreach (var img in message.Images)
            {
                if (!string.IsNullOrEmpty(img.ImageUrl?.Url))
                    items.Add(new SKImageContent(new Uri(img.ImageUrl.Url)));
            }
        }

        var metadata = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(message.Reasoning))
            metadata["Reasoning"] = message.Reasoning;

        return new ChatMessageContent(role, items, modelId, metadata: metadata.Count > 0 ? metadata : null);
    }

    public static ChatHistory ToChatHistory(IEnumerable<LLMMessage> messages, string modelId = null)
    {
        var history = new ChatHistory();
        foreach (var msg in messages)
            history.Add(ToSKMessage(msg, modelId));
        return history;
    }

    private static AuthorRole ToAuthorRole(string role) => role?.ToLowerInvariant() switch
    {
        "system" => AuthorRole.System,
        "assistant" => AuthorRole.Assistant,
        "tool" => AuthorRole.Tool,
        _ => AuthorRole.User,
    };
}
