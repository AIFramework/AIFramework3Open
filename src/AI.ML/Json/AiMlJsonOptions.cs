using AI.DataStructs.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.ML.Json;

/// <summary>
/// Предварительно сконфигурированные <see cref="JsonSerializerOptions"/> для сериализации
/// ML-моделей через <see cref="AI.DataStructs.SafeSerializer"/>.
/// </summary>
public static class AiMlJsonOptions
{
    /// <summary>
    /// Для классификаторов (NN, CorrelationClassifier, StructClasses, BaseClassifier).
    /// Включает конвертеры для Vector и StructClasses.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new VectorJsonConverter(),
            new StructClassesJsonConverter(),
        },
    };

    /// <summary>
    /// Для MCFast (марковские цепи). Включает MCFastJsonConverter и VectorJsonConverter.
    /// </summary>
    public static readonly JsonSerializerOptions MCFastOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new VectorJsonConverter(),
            new MCFastJsonConverter(),
        },
    };
}
