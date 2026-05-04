using AI.DataStructs.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.KNN.Json;

/// <summary>
/// Предварительно сконфигурированные <see cref="JsonSerializerOptions"/> для сериализации
/// KNN-моделей через <see cref="AI.DataStructs.SafeSerializer"/>.
/// </summary>
public static class AiKnnJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = false,
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new VectorJsonConverter(),
            new KNNClJsonConverter(),
        },
    };
}
