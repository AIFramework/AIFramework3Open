using AI.DataStructs.Data;
using AI.ML.SequenceAnalysis.HMM;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.ML.Json;

/// <summary>
/// Кастомный конвертер для <see cref="MCFast"/>.
/// Сериализует <c>Dictionary&lt;int[], Dictionary&lt;int, double&gt;&gt;</c>
/// как JSON-массив объектов вида <c>{"k":[1,2],"c":{"4":0.1,"5":0.2}}</c>.
/// После десериализации вызывает <c>ReconstructAfterLoad()</c> для восстановления _map.
/// </summary>
internal sealed class MCFastJsonConverter : JsonConverter<MCFast>
{
    public override MCFast Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var mc = new MCFast();

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for MCFast");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            string prop = reader.GetString()!;
            reader.Read();

            switch (prop)
            {
                case "NGram":           mc.NGram       = reader.GetInt32();  break;
                case "StartToken":      mc.StartToken  = reader.GetInt32();  break;
                case "EndToken":        mc.EndToken    = reader.GetInt32();  break;
                case "ProbabilityVector":
                    mc.ProbabilityVector = JsonSerializer.Deserialize<AI.DataStructs.Algebraic.Vector>(ref reader, options)!;
                    break;
                case "Data":
                    ReadData(ref reader, mc, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        mc.ReconstructAfterLoad();
        return mc;
    }

    private static void ReadData(ref Utf8JsonReader reader, MCFast mc, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray for MCFast.Data");

        var dict = mc.InternalData;
        dict.Clear();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject) continue;

            int[]? key = null;
            Dictionary<int, double>? continuations = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                string p = reader.GetString()!;
                reader.Read();

                if (p == "k")
                    key = JsonSerializer.Deserialize<int[]>(ref reader, options)!;
                else if (p == "c")
                    continuations = ReadContinuations(ref reader);
                else
                    reader.Skip();
            }

            if (key != null && continuations != null)
                dict[key] = continuations;
        }
    }

    private static Dictionary<int, double> ReadContinuations(ref Utf8JsonReader reader)
    {
        var result = new Dictionary<int, double>();
        if (reader.TokenType != JsonTokenType.StartObject) return result;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            int token = int.Parse(reader.GetString()!);
            reader.Read();
            result[token] = reader.GetDouble();
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, MCFast value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("NGram",      value.NGram);
        writer.WriteNumber("StartToken", value.StartToken);
        writer.WriteNumber("EndToken",   value.EndToken);

        writer.WritePropertyName("ProbabilityVector");
        if (value.ProbabilityVector != null)
            JsonSerializer.Serialize(writer, value.ProbabilityVector, options);
        else
            writer.WriteNullValue();

        writer.WritePropertyName("Data");
        writer.WriteStartArray();
        foreach (var kvp in value.InternalData)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("k");
            JsonSerializer.Serialize(writer, kvp.Key, options);
            writer.WritePropertyName("c");
            writer.WriteStartObject();
            foreach (var cont in kvp.Value)
            {
                writer.WriteNumber(cont.Key.ToString(), cont.Value);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
