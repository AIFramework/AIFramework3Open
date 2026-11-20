using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Interop.Effects;

/// <summary>
/// Ключ эффекта (<see cref="ToolCall.EffectKey"/>): единственное место во фреймворке, где он создается.
/// </summary>
/// <remarks>
/// <para>
/// Ключ — точная идентичность эффекта: одинаковые идентификатор запуска, имя инструмента и аргументы дают один
/// ключ, любое отличие в значениях — другой. Аргументы приводятся к каноническому JSON: ключи объектов
/// отсортированы порядковым сравнением, пробелов вне строк нет, строки и числа не меняются (регистр и пробелы
/// внутри строк сохраняются, число пишется тем же текстом, что пришел). Поэтому другой порядок полей и другие
/// пробелы между токенами дают тот же ключ, а другой регистр или пробел внутри строкового значения — другой.
/// Имя инструмента сравнивается порядковым сравнением.
/// </para>
/// <para>
/// <c>ReActActionKey</c> из AI.LLM здесь сознательно не используется: это эвристика, по которой движок ReAct
/// узнает, что модель повторяет действие, и она не различает регистр и схлопывает пробелы. Для эффекта такое
/// совпадение недопустимо: второй вызов с другим текстом письма не исполнился бы, а вернул итог первого.
/// </para>
/// <para>
/// Наружу уходит не сама каноническая строка, а SHA-256 от нее (64 шестнадцатеричных знака): инструмент с
/// <see cref="ToolIdempotency.WithKey"/> передает ключ внешнему сервису, и аргументы не должны утечь в
/// заголовок, а длина ключа должна быть постоянной.
/// </para>
/// </remarks>
public static class EffectKeys
{
    /// <summary>Создает ключ эффекта.</summary>
    /// <param name="runId">Идентификатор запуска (<see cref="ToolContext.RunId"/>).</param>
    /// <param name="toolName">Имя инструмента.</param>
    /// <param name="arguments">Аргументы вызова; <see cref="JsonValueKind.Undefined"/> — пустые.</param>
    /// <returns>Ключ: 64 строчных шестнадцатеричных знака.</returns>
    public static string Create(string runId, string toolName, JsonElement arguments)
    {
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(toolName);

        string canonical = arguments.ValueKind == JsonValueKind.Undefined ? string.Empty : Canonical(arguments);

        // Длина перед каждой частью вместо разделителя: части не «перетекают» друг в друга.
        string material = Part(runId) + Part(toolName) + canonical;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexStringLower(hash);
    }

    private static string Part(string value) =>
        value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value + ":";

    private static string Canonical(JsonElement value)
    {
        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            Write(writer, value);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                // Сортировка устойчива: повторяющиеся имена сохраняют исходный порядок между собой.
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                // Значение строки как есть; экранирование у писателя одно и то же, поэтому экранированная и обычная
                // запись одного знака совпадают.
                writer.WriteStringValue(value.GetString());
                break;

            case JsonValueKind.Number:
                // Текст числа без изменений: 1 и 1.0 — разные аргументы.
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(value.ValueKind == JsonValueKind.True);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                throw new ArgumentException($"Неожиданный вид значения JSON: {value.ValueKind}.", nameof(value));
        }
    }
}
