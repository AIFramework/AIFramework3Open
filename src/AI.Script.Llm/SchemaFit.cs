using AI.Script.Runtime;
using AI.Script.Std;
using System.Globalization;
using System.Text;

namespace AI.Script.Llm;

/// <summary>
/// Приведение ответа модели к объявленной схеме.
/// </summary>
/// <remarks>
/// Модель отвечает «сумма: "1 234,50 ₽"» там, где просили число, и «инн: 7701234567» там, где
/// просили строку. Принимать это как есть — значит ронять скрипт двумя строками ниже, на
/// сравнении или на сложении; требовать буквального совпадения — терять каждый десятый ответ
/// на форматировании вместо существа. Поэтому значение приводится по объявленному типу, а
/// непереводимое становится отказом с именем поля.
/// <para>
/// Схема — запись «поле → имя типа», те же имена, что и в языке: <c>str</c>, <c>num</c>,
/// <c>dec</c>, <c>bool</c>, <c>date</c>, <c>list</c>, <c>record</c>. Отдельного языка описания
/// схем нет намеренно: второй словарь типов разошёлся бы с первым.
/// </para>
/// </remarks>
public static class SchemaFit
{
    /// <summary>Приводит ответ к схеме; <c>false</c> — вместе с причиной для второй попытки.</summary>
    /// <param name="value">Ответ модели.</param>
    /// <param name="schema">Схема: поле и его вид.</param>
    /// <param name="fitted">Ответ, приведенный к схеме.</param>
    /// <param name="error">Причина отказа.</param>
    /// <param name="missing">
    /// Пропущенное поле допустимо и становится пропуском. Так при извлечении: в тексте поля может
    /// не быть, модели велено вернуть null, и строка из-за одного пустого поля терялась целиком.
    /// </param>
    public static bool TryFit(ScriptValue value, ScriptRecord schema, out ScriptValue fitted, out string error, bool missing = false)
    {
        fitted = value;
        error = string.Empty;

        if (value.Type != ScriptType.Record)
        {
            error = $"ожидался объект с полями {string.Join(", ", schema.Keys)}, пришло значение типа {value.Type.ToName()}";
            return false;
        }

        ScriptRecord answer = value.AsRecord();
        var fields = new List<KeyValuePair<string, ScriptValue>>(schema.Count);
        var problems = new List<string>();

        foreach (var pair in schema.Pairs())
        {
            string expected = Expected(pair.Value);

            if (!answer.TryGet(pair.Key, out ScriptValue actual) || actual.IsNone)
            {
                if (missing) fields.Add(new KeyValuePair<string, ScriptValue>(pair.Key, ScriptValue.None));
                else problems.Add($"нет поля «{pair.Key}» ({expected})");

                continue;
            }

            if (TryConvert(actual, expected, out ScriptValue converted))
            {
                fields.Add(new KeyValuePair<string, ScriptValue>(pair.Key, converted));
                continue;
            }

            problems.Add($"поле «{pair.Key}»: ожидался {expected}, пришло {Show(actual)}");
        }

        if (problems.Count > 0)
        {
            error = string.Join("; ", problems);
            return false;
        }

        // Лишние поля не мешают: модель часто добавляет пояснение, и выбрасывать из-за него
        // верный ответ значило бы платить второй раз за то же самое.
        foreach (var pair in answer.Pairs())
        {
            if (!schema.Has(pair.Key)) fields.Add(pair);
        }

        fitted = ScriptValue.Record(ScriptRecord.From(fields));

        return true;
    }

    /// <summary>Описание схемы для промпта: что и в каком виде ждём.</summary>
    public static string Describe(ScriptRecord schema)
    {
        var builder = new StringBuilder();

        foreach (var pair in schema.Pairs())
        {
            if (builder.Length > 0) _ = builder.Append(", ");

            _ = builder.Append(pair.Key).Append(" (").Append(Words(Expected(pair.Value))).Append(')');
        }

        return builder.ToString();
    }

    private static string Expected(ScriptValue declared) =>
        declared.Type == ScriptType.Str ? declared.AsString().Trim().ToLowerInvariant() : declared.Type.ToName();

    private static string Words(string expected) => expected switch
    {
        "str" => "строка",
        "num" => "число",
        "dec" => "точное число",
        "bool" => "true либо false",
        "date" => "дата в виде ГГГГ-ММ-ДД",
        "list" => "список",
        "record" => "объект",
        _ => expected,
    };

    private static bool TryConvert(ScriptValue value, string expected, out ScriptValue converted)
    {
        converted = value;

        switch (expected)
        {
            case "any":
                return true;

            case "str":
                converted = value.Type == ScriptType.Str ? value : ScriptValue.Str(ScriptFormatter.Format(value));
                return true;

            case "num":
                if (value.Type == ScriptType.Num) return true;
                if (value.Type == ScriptType.Dec) { converted = ScriptValue.Num((double)value.AsDecimal()); return true; }
                if (value.Type != ScriptType.Str) return false;

                if (DecimalText.TryParse(value.AsString(), "ru", out decimal number, out _))
                {
                    converted = ScriptValue.Num((double)number);
                    return true;
                }

                return false;

            case "dec":
                if (value.Type == ScriptType.Dec) return true;
                if (value.Type == ScriptType.Num) { converted = ScriptValue.Dec((decimal)value.RawNumber); return true; }
                if (value.Type != ScriptType.Str) return false;

                if (DecimalText.TryParse(value.AsString(), "ru", out decimal exact, out _))
                {
                    converted = ScriptValue.Dec(exact);
                    return true;
                }

                return false;

            case "bool":
                if (value.Type == ScriptType.Bool) return true;
                if (value.Type != ScriptType.Str) return false;

                string flag = value.AsString().Trim().ToLowerInvariant();

                if (flag is "true" or "да" or "yes" or "1") { converted = ScriptValue.True; return true; }
                if (flag is "false" or "нет" or "no" or "0") { converted = ScriptValue.False; return true; }

                return false;

            case "date":
                if (value.Type == ScriptType.Date) return true;
                if (value.Type != ScriptType.Str) return false;

                // Сперва ISO, затем русская запись: «01.02.2026» это первое февраля, а инвариантная
                // культура прочла бы ее как второе января.
                if (DateTime.TryParseExact(value.AsString().Trim(), ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ"],
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime moment)
                    || DateTime.TryParse(value.AsString(), CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out moment)
                    || DateTime.TryParse(value.AsString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out moment))
                {
                    converted = ScriptValue.Date(moment);
                    return true;
                }

                return false;

            case "list":
                return value.Type is ScriptType.List or ScriptType.Vec;

            case "record":
                return value.Type == ScriptType.Record;

            default:
                return true;
        }
    }

    private static string Show(ScriptValue value)
    {
        string text = ScriptFormatter.Format(value, quoteStrings: true);

        return text.Length <= 40 ? text : text[..40] + "…";
    }
}
