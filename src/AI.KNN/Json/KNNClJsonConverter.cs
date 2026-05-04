using AI.KNN;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.KNN.Json;

/// <summary>
/// Кастомный конвертер для <see cref="KNNCl"/> — сериализует приватные массивы
/// (_features, _labels, _count, _dim) вместе с публичными параметрами.
/// </summary>
internal sealed class KNNClJsonConverter : JsonConverter<KNNCl>
{
    public override KNNCl Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var knn = new KNNCl();

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            string prop = reader.GetString()!;
            reader.Read();

            switch (prop)
            {
                case "K":               knn.K               = reader.GetInt32();   break;
                case "H":               knn.H               = reader.GetDouble();  break;
                case "IsFixed":         knn.IsFixed         = reader.GetBoolean(); break;
                case "IsParsenMethod":  knn.IsParsenMethod  = reader.GetBoolean(); break;
                case "_count":          knn.InternalCount   = reader.GetInt32();   break;
                case "_dim":            knn.InternalDim     = reader.GetInt32();   break;
                case "_features":
                    knn.InternalFeatures = JsonSerializer.Deserialize<float[]>(ref reader, options) ?? [];
                    break;
                case "_labels":
                    knn.InternalLabels = JsonSerializer.Deserialize<int[]>(ref reader, options) ?? [];
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        knn.RebuildClassStats();
        return knn;
    }

    public override void Write(Utf8JsonWriter writer, KNNCl value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("K",              value.K);
        writer.WriteNumber("H",              value.H);
        writer.WriteBoolean("IsFixed",       value.IsFixed);
        writer.WriteBoolean("IsParsenMethod",value.IsParsenMethod);
        writer.WriteNumber("_count",         value.InternalCount);
        writer.WriteNumber("_dim",           value.InternalDim);
        writer.WritePropertyName("_features");
        JsonSerializer.Serialize(writer, value.InternalFeatures, options);
        writer.WritePropertyName("_labels");
        JsonSerializer.Serialize(writer, value.InternalLabels, options);
        writer.WriteEndObject();
    }
}
