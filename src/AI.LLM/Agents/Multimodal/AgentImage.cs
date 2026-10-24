using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Messages.Content;

namespace AI.LLM.Agents.Multimodal;

/// <summary>
/// Изображение, передаваемое агенту или возвращаемое инструментом.
/// Используется в мультимодальном цикле Observe-Reason-Act.
/// </summary>
public sealed class AgentImage
{
    /// <summary>Сырые байты изображения.</summary>
    public byte[] Data { get; }

    /// <summary>MIME-тип (например, "image/png", "image/jpeg").</summary>
    public string MimeType { get; }

    /// <summary>Семантическая метка: "screenshot", "camera_front", "depth_map" и т.д.</summary>
    public string Label { get; }

    public AgentImage(byte[] data, string mimeType = "image/jpeg", string label = null)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        MimeType = mimeType ?? "image/jpeg";
        Label = label;
    }

    /// <summary>Возвращает data URI для вставки в LLM-запрос.</summary>
    internal string ToDataUri() => $"data:{MimeType};base64,{Convert.ToBase64String(Data)}";

    /// <summary>
    /// Сообщение пользователя: текст и изображения (multimodal <see cref="MessageContent"/>); без изображений —
    /// обычное текстовое.
    /// </summary>
    internal static LLMMessage UserMessage(string text, IReadOnlyList<AgentImage> images)
    {
        if (images is not { Count: > 0 })
            return LLMMessage.CreateMessage(Roles.User, text);

        var content = new MessageContent(text);
        foreach (AgentImage image in images)
            content.AddImage(image.Data);
        return new LLMMessage(LLMMessage.UserRole, content);
    }
}
