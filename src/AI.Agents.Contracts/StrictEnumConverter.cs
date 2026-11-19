using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.Agents.Contracts;

/// <summary>
/// Перечисление контракта в JSON: пишется именем значения, читается только по имени.
/// </summary>
/// <remarks>
/// Подключается атрибутом на самом перечислении, поэтому вызывающему ничего настраивать не нужно.
/// Отличие от <see cref="JsonStringEnumConverter{TEnum}"/>: число на входе не принимается, иначе «5»
/// или «0» молча стали бы значением, которого никто не выбирал. Неизвестное имя дает
/// <see cref="JsonException"/> со списком допустимых имен. Регистр имени при чтении не важен,
/// как и у встроенного конвертера; пишется имя ровно так, как оно объявлено.
/// </remarks>
/// <typeparam name="TEnum">Перечисление контракта.</typeparam>
internal sealed class StrictEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly string[] Names = Enum.GetNames<TEnum>();

    /// <inheritdoc />
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                $"{typeof(TEnum).Name}: ожидается строка с именем значения ({string.Join(", ", Names)}), " +
                $"а пришло {reader.TokenType}.");
        }

        string? text = reader.GetString();

        foreach (string name in Names)
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
                return Enum.Parse<TEnum>(name);
        }

        throw new JsonException(
            $"{typeof(TEnum).Name}: неизвестное значение «{text}». Допустимы: {string.Join(", ", Names)}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value))
        {
            throw new JsonException(
                $"{typeof(TEnum).Name}: значение {value} не объявлено в перечислении и не может быть записано.");
        }

        writer.WriteStringValue(value.ToString());
    }
}
