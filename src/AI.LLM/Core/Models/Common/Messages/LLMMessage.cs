using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;
using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Messages;

/// <summary>
/// Represents a chat message from different roles (e.g., "user", "assistant").
/// </summary>
[Serializable]
public class LLMMessage
{
    [JsonIgnore]
    public const string UserRole = "user";
    [JsonIgnore]
    public const string AssistantRole = "assistant";
    [JsonIgnore]
    public const string SystemRole = "system";

    /// <summary>
    /// Gets the role of the message sender (e.g., "user" or "assistant").
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the content of the message (can be null).
    /// </summary>
    [JsonPropertyName("content")]
    [JsonConverter(typeof(ContentJsonConverter))]
    public object Content { get; set; }


    /// <summary>
    /// A list of images included in the message.
    /// Will be null or empty for text-only responses.
    /// </summary>
    [JsonPropertyName("images")]
    public List<ImageInfo> Images { get; set; }

    [JsonPropertyName("refusal")]
    public string Refusal { get; set; }

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; }

    /// <summary>
    /// Список вызовов инструментов, запрошенных ассистентом (role=assistant).
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public List<ToolCall> ToolCalls { get; set; }

    /// <summary>
    /// Идентификатор вызова, на который отвечает данное сообщение (role=tool).
    /// </summary>
    [JsonPropertyName("tool_call_id")]
    public string ToolCallId { get; set; }

    /// <summary>
    /// Имя функции/инструмента (для role=tool).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    public const string ToolRole = "tool";

    /// <summary>
    /// Создаёт сообщение с результатом вызова инструмента (role=tool).
    /// </summary>
    public static LLMMessage CreateToolResult(string toolCallId, string content)
    {
        return new LLMMessage
        {
            Role = ToolRole,
            Content = content,
            ToolCallId = toolCallId,
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Message"/> class.
    /// </summary>
    public LLMMessage() // Parameterless constructor for deserialization
    {
        Role = string.Empty;
        Content = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LLMMessage"/> class with the specified role and content.
    /// </summary>
    /// <param name="role">The role of the message sender (e.g., "user", "assistant").</param>
    /// <param name="content">The text content of the message (can be null).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="role"/> is null or whitespace.</exception>
    public LLMMessage(string role, string content) => InitText(role, content);

    public LLMMessage(string role, MessageContent content) => InitMC(role, content);

    public LLMMessage(string role, object content)
    {
        if (content is string)
            InitText(role, content as string);
        else if (content is MessageContent)
            InitMC(role, content as MessageContent);
        else throw new ArgumentException("Не поддерживаемый тип контента, используйте string или MessageContent", nameof(content));
    }


    // Инициализация строкой
    private void InitText(string role, string content)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or whitespace.", nameof(role));

        Role = role;
        Content = content;
    }

    // Инициализация MessageContent
    private void InitMC(string role, MessageContent content)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or whitespace.", nameof(role));

        Role = role;
        if (role == "user")
            Content = content;
        else
            Content = content.ToString();
    }

    /// <summary>
    /// Creates a message for sending to the LLM API.
    /// </summary>
    /// <param name="role">The role of the sender.</param>
    /// <param name="content">The message content (can be null).</param>
    /// <returns>A new <see cref="LLMMessage"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="role"/> is invalid.</exception>
    public static LLMMessage CreateMessage(Roles role, string content)
    {
        var senderRole = role.ToString().ToLower();
        return new LLMMessage(senderRole, content);
    }

    /// <summary>
    /// Creates a deep copy of the <see cref="LLMMessage"/> instance.
    /// </summary>
    /// <returns>A new <see cref="LLMMessage"/> instance with the same properties.</returns>
    public LLMMessage DeepClone()
    {
        if (Content is string s)
            return new LLMMessage(Role, s);
        if (Content is MessageContent mc)
            return new LLMMessage(Role, mc);
        
        var clone = new LLMMessage { Role = Role, Content = Content };
        if (Images != null)
            clone.Images = new List<ImageInfo>(Images);
        if (Reasoning != null)
            clone.Reasoning = Reasoning;
        return clone;
    }


}
