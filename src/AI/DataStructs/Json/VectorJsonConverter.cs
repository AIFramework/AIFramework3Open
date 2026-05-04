using AI.DataStructs.Algebraic;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.DataStructs.Json;

/// <summary>
/// Сериализует <see cref="Vector"/> как JSON-массив double.
/// </summary>
public sealed class VectorJsonConverter : JsonConverter<Vector>
{
    public override Vector Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        double[]? arr = JsonSerializer.Deserialize<double[]>(ref reader, options);
        return arr is null ? new Vector(0) : new Vector(arr);
    }

    public override void Write(Utf8JsonWriter writer, Vector value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value.ToArray(), options);
}
