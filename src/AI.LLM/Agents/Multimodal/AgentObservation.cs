namespace AI.LLM.Agents.Multimodal;

/// <summary>
/// Наблюдение окружающей среды: изображения + текстовая метадата.
/// Возвращается <see cref="IObservationProvider"/> между шагами агента.
/// </summary>
public sealed class AgentObservation
{
    /// <summary>Изображения наблюдения (скриншот, камера, датчики глубины).</summary>
    public IReadOnlyList<AgentImage> Images { get; }

    /// <summary>Текстовое описание: координаты курсора, показания датчиков и т.д.</summary>
    public string Description { get; }

    /// <summary>Метка времени наблюдения.</summary>
    public DateTimeOffset Timestamp { get; }

    public AgentObservation(IReadOnlyList<AgentImage> images, string description = null)
    {
        Images = images ?? [];
        Description = description;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public AgentObservation(AgentImage image, string description = null)
        : this(image != null ? [image] : [], description) { }
}
