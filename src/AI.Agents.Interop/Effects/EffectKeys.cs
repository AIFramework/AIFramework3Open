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
/// отсортированы порядковым сравнением, пробелов вне строк нет, строки не меняются (регистр и пробелы внутри
/// строк сохраняются), порядок элементов массива сохраняется. Поэтому другой порядок полей и другие пробелы между
/// токенами дают тот же ключ, а другой регистр или пробел внутри строкового значения — другой. Имя инструмента
/// сравнивается порядковым сравнением.
/// </para>
/// <para>
/// Числа сравниваются по значению: <c>100</c>, <c>100.0</c>, <c>1e2</c> и <c>1E+2</c> дают один ключ, как и
/// <c>0</c>, <c>0.0</c> и <c>-0</c>, как и <c>0.1</c> и <c>0.10</c>. Число приводится по своей десятичной записи,
/// без <see cref="double"/> и <see cref="decimal"/>: значащие цифры без ведущих и хвостовых нулей и порядок,
/// <c>[-]цифры e порядок</c> (ноль — <c>0</c>). Так значение определяется точно при любом числе цифр, и разные
/// значения никогда не дают один ключ: <c>9007199254740993</c> и <c>9007199254740992</c>, неразличимые в
/// <see cref="double"/>, или числа длиннее точности <see cref="decimal"/> остаются разными. Если порядок не
/// помещается в <see cref="long"/> (больше 18 цифр), число сохраняется своей записью без приведения: лучше
/// лишний вызов, чем пропущенное действие.
/// </para>
/// <para>
/// <c>ReActActionKey</c> из AI.LLM здесь сознательно не используется: это эвристика, по которой движок ReAct
/// узнает, что модель повторяет действие, и она не различает регистр и схлопывает пробелы. Для эффекта такое
/// совпадение недопустимо: второй вызов с другим текстом письма не исполнился бы, а вернул итог первого.
/// </para>
/// <para>
/// Наружу уходит не сама каноническая строка, а SHA-256 от нее: инструмент с <see cref="ToolIdempotency.WithKey"/>
/// передает ключ внешнему сервису, и аргументы не должны утечь в заголовок, а длина ключа должна быть постоянной.
/// Значение — версия формулы <see cref="Version"/> (<c>ek1:</c>) и 64 строчных шестнадцатеричных знака. Любое
/// изменение формулы, после которого прежние входы дают другие ключи, меняет версию (<c>ek2:</c>): ключи разных
/// версий не совпадают, и журнал старого запуска не примет чужой ключ за свой.
/// </para>
/// </remarks>
public static class EffectKeys
{
    /// <summary>Префикс версии формулы ключа; меняется только решением владельца вместе с эталонами.</summary>
    public const string Version = "ek1:";

    // Порядок в 18 цифр помещается в long с запасом на поправку к длине записи числа.
    private const int MaxExponentDigits = 18;

    /// <summary>Создает ключ эффекта.</summary>
    /// <param name="runId">Идентификатор запуска (<see cref="ToolContext.RunId"/>).</param>
    /// <param name="toolName">Имя инструмента.</param>
    /// <param name="arguments">Аргументы вызова; <see cref="JsonValueKind.Undefined"/> — пустые.</param>
    /// <returns>Ключ: <see cref="Version"/> и 64 строчных шестнадцатеричных знака.</returns>
    public static string Create(string runId, string toolName, JsonElement arguments)
    {
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(toolName);

        string canonical = arguments.ValueKind == JsonValueKind.Undefined ? string.Empty : Canonical(arguments);

        // Длина перед каждой частью вместо разделителя: части не «перетекают» друг в друга.
        string material = Part(runId) + Part(toolName) + canonical;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Version + Convert.ToHexStringLower(hash);
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
                // Значение числа, а не его запись: 1, 1.0 и 1e0 — один аргумент.
                writer.WriteRawValue(CanonicalNumber(value.GetRawText()), skipInputValidation: true);
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

    /// <summary>
    /// Каноническая запись числа JSON по значению: <c>[-]цифры e порядок</c> без ведущих и хвостовых нулей, ноль —
    /// <c>0</c>; число с порядком длиннее <see cref="MaxExponentDigits"/> цифр — исходной записью.
    /// </summary>
    /// <param name="text">Запись числа, уже проверенная читателем JSON.</param>
    private static string CanonicalNumber(string text)
    {
        bool negative = text[0] == '-';
        string unsigned = negative ? text[1..] : text;

        int e = unsigned.IndexOfAny(['e', 'E']);
        string mantissa = e < 0 ? unsigned : unsigned[..e];
        long exponent = 0;

        if (e >= 0)
        {
            string power = unsigned[(e + 1)..];
            bool minus = power[0] == '-';
            string powerDigits = power.TrimStart('+', '-').TrimStart('0');

            // Значение такого числа определено, но порядок не помещается в long: запись остается как есть.
            if (powerDigits.Length > MaxExponentDigits) return text;

            exponent = powerDigits.Length == 0 ? 0 : long.Parse(powerDigits, NumberStyles.None, CultureInfo.InvariantCulture);
            if (minus) exponent = -exponent;
        }

        int dot = mantissa.IndexOf('.');
        string digits = dot < 0 ? mantissa : mantissa[..dot] + mantissa[(dot + 1)..];
        if (dot >= 0) exponent -= mantissa.Length - dot - 1;

        digits = digits.TrimStart('0');
        if (digits.Length == 0) return "0";

        string significant = digits.TrimEnd('0');
        exponent += digits.Length - significant.Length;

        return (negative ? "-" : string.Empty) + significant + "e" + exponent.ToString(CultureInfo.InvariantCulture);
    }
}
