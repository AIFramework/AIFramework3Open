using AI.ML.DataHandling.DataSets;
using AI.ML.Classification;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.ML.Json;

/// <summary>
/// Сериализует <see cref="StructClasses"/> как JSON-массив <see cref="VectorDatasetItem"/>.
/// Нужен потому что <see cref="StructClasses"/> наследует List с private <c>ReaderWriterLockSlim</c>.
/// </summary>
public sealed class StructClassesJsonConverter : JsonConverter<StructClasses>
{
    public override StructClasses Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var items = JsonSerializer.Deserialize<List<VectorDatasetItem>>(ref reader, options)
                    ?? new List<VectorDatasetItem>();
        var result = new StructClasses();
        foreach (var item in items)
            result.Add(item);
        return result;
    }

    public override void Write(Utf8JsonWriter writer, StructClasses value, JsonSerializerOptions options)
    {
        // Thread-safe snapshot через индексный доступ (StructClasses использует ReadLock)
        var snapshot = new List<VectorDatasetItem>(value.Count);
        for (int i = 0; i < value.Count; i++)
            snapshot.Add(value[i]);
        JsonSerializer.Serialize(writer, snapshot, options);
    }
}
