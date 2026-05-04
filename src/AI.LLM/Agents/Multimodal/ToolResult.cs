namespace AI.LLM.Agents.Multimodal;

/// <summary>
/// Мультимодальный результат инструмента: текст + опциональные изображения.
/// Инструменты, возвращающие этот тип, автоматически передают изображения
/// в визуальный контекст агента.
/// <example>
/// <code>
/// [AgentTool("capture_camera", "Захватывает кадр с камеры робота")]
/// public ToolResult CaptureCamera()
/// {
///     var frame = _camera.Capture();
///     return new ToolResult("Кадр захвачен", new AgentImage(frame, "image/jpeg", "camera"));
/// }
/// </code>
/// </example>
/// </summary>
public sealed class ToolResult
{
    /// <summary>Текстовая часть результата.</summary>
    public string Text { get; }

    /// <summary>Изображения, возвращённые инструментом.</summary>
    public IReadOnlyList<AgentImage> Images { get; }

    /// <summary>Есть ли изображения в результате.</summary>
    public bool HasImages => Images is { Count: > 0 };

    public ToolResult(string text, IReadOnlyList<AgentImage> images = null)
    {
        Text = text ?? string.Empty;
        Images = images ?? [];
    }

    public ToolResult(string text, params AgentImage[] images)
        : this(text, (IReadOnlyList<AgentImage>)images) { }

    /// <summary>Неявное преобразование из строки (без изображений).</summary>
    public static implicit operator ToolResult(string text) => new(text);
}
