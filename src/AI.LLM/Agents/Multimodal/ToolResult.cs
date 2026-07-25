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

    /// <summary>
    /// Инструмент отработал успешно.
    /// <para>
    /// Инструменту нужен способ сказать «не смог», не бросая исключение: отказ вроде
    /// «файл не найден» или «команда заблокирована» — это нормальный ход событий, а не сбой.
    /// Без такого признака ошибка возвращается обычной строкой и считается успехом, а
    /// вызывающей стороне остаётся угадывать неудачу по тексту — приём хрупкий и
    /// привязанный к языку сообщения.
    /// </para>
    /// </summary>
    public bool IsSuccess { get; }

    public ToolResult(string text, IReadOnlyList<AgentImage> images = null, bool isSuccess = true)
    {
        Text = text ?? string.Empty;
        Images = images ?? [];
        IsSuccess = isSuccess;
    }

    public ToolResult(string text, params AgentImage[] images)
        : this(text, (IReadOnlyList<AgentImage>)images) { }

    /// <summary>Неудача инструмента: текст объясняет модели, что именно не получилось.</summary>
    /// <param name="text">Описание отказа.</param>
    /// <param name="images">Изображения, если они помогают понять причину.</param>
    public static ToolResult Failure(string text, IReadOnlyList<AgentImage> images = null) =>
        new(text, images, isSuccess: false);

    /// <summary>Успешный результат.</summary>
    /// <param name="text">Текст результата.</param>
    /// <param name="images">Изображения.</param>
    public static ToolResult Success(string text, IReadOnlyList<AgentImage> images = null) =>
        new(text, images);

    /// <summary>Неявное преобразование из строки (успешный результат без изображений).</summary>
    public static implicit operator ToolResult(string text) => new(text);
}
