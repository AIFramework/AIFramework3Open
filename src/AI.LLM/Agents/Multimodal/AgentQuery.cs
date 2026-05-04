namespace AI.LLM.Agents.Multimodal;

/// <summary>
/// Мультимодальный запрос к агенту: текст + опциональные изображения.
/// </summary>
public sealed class AgentQuery
{
    /// <summary>Текстовая часть запроса.</summary>
    public string Text { get; }

    /// <summary>Изображения, приложенные к запросу (камера, скриншот и т.д.).</summary>
    public IReadOnlyList<AgentImage> Images { get; }

    /// <summary>Есть ли изображения в запросе.</summary>
    public bool HasImages => Images is { Count: > 0 };

    public AgentQuery(string text, IReadOnlyList<AgentImage> images = null)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Images = images ?? [];
    }

    public AgentQuery(string text, params AgentImage[] images)
        : this(text, (IReadOnlyList<AgentImage>)images) { }

    /// <summary>Неявное преобразование из строки.</summary>
    public static implicit operator AgentQuery(string text) => new(text);
}
