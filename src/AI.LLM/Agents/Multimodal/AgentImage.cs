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
}
