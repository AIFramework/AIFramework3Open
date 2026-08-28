using AI.DataStructs.Algebraic;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AI.Script.Std;

/// <summary>
/// Перевод между JSON и значениями языка.
/// </summary>
/// <remarks>
/// Объект становится записью, массив — списком, число — <c>num</c>, <c>null</c> — <c>none</c>.
/// Массив из одних чисел остаётся списком, а не вектором: превращать его в вектор значило бы
/// решать за автора скрипта, что это данные, а не, например, три разнородных параметра.
/// </remarks>
public static class Json
{
    /// <summary>Предельная глубина вложенности при разборе.</summary>
    public const int MaxDepth = 64;

    /// <summary>Разбирает JSON в значение языка.</summary>
    public static ScriptValue Parse(string text, string source)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = MaxDepth,
            });

            return Convert(document.RootElement);
        }
        catch (JsonException exception)
        {
            throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"{source}: не удалось разобрать JSON — {exception.Message}");
        }
    }

    /// <summary>Печатает значение языка в JSON.</summary>
    public static string Write(ScriptValue value, bool pretty)
    {
        var builder = new StringBuilder();

        Append(builder, value, pretty, 0);

        return builder.ToString();
    }

    private static ScriptValue Convert(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var fields = new List<KeyValuePair<string, ScriptValue>>();

                    foreach (JsonProperty property in element.EnumerateObject())
                        fields.Add(new KeyValuePair<string, ScriptValue>(property.Name, Convert(property.Value)));

                    return ScriptValue.Record(ScriptRecord.From(fields));
                }

            case JsonValueKind.Array:
                {
                    var items = new List<ScriptValue>();

                    foreach (JsonElement item in element.EnumerateArray()) items.Add(Convert(item));

                    return ScriptValue.List(ScriptList.From(items));
                }

            case JsonValueKind.String:
                return ScriptValue.Str(element.GetString() ?? string.Empty);

            case JsonValueKind.Number:
                return ScriptValue.Num(element.GetDouble());

            case JsonValueKind.True:
                return ScriptValue.True;

            case JsonValueKind.False:
                return ScriptValue.False;

            default:
                return ScriptValue.None;
        }
    }

    private static void Append(StringBuilder builder, ScriptValue value, bool pretty, int depth)
    {
        switch (value.Type)
        {
            case ScriptType.None:
                _ = builder.Append("null");
                return;

            case ScriptType.Bool:
                _ = builder.Append(value.RawNumber != 0 ? "true" : "false");
                return;

            case ScriptType.Num:
                AppendNumber(builder, value.RawNumber);
                return;

            case ScriptType.Str:
                AppendString(builder, value.AsString());
                return;

            case ScriptType.Date:
                AppendString(builder, value.AsDate().ToString("O", CultureInfo.InvariantCulture));
                return;

            case ScriptType.Dur:
                AppendNumber(builder, value.AsDuration().TotalSeconds);
                return;

            case ScriptType.Vec:
                {
                    Vector vector = value.AsVector();
                    var items = new ScriptValue[vector.Count];

                    for (int i = 0; i < vector.Count; i++) items[i] = ScriptValue.Num(vector[i]);

                    AppendArray(builder, ScriptList.Own(items), pretty, depth);
                    return;
                }

            case ScriptType.Range:
                {
                    var items = new List<ScriptValue>();

                    foreach (double number in value.AsRange().Values()) items.Add(ScriptValue.Num(number));

                    AppendArray(builder, ScriptList.From(items), pretty, depth);
                    return;
                }

            case ScriptType.List:
                AppendArray(builder, value.AsList(), pretty, depth);
                return;

            case ScriptType.Record:
                AppendObject(builder, value.AsRecord().Pairs(), value.AsRecord().Count, pretty, depth);
                return;

            case ScriptType.Table:
                {
                    ScriptTable table = value.AsTable();
                    var fields = new List<KeyValuePair<string, ScriptValue>>(table.ColumnCount);

                    foreach (ScriptColumn column in table.Columns)
                        fields.Add(new KeyValuePair<string, ScriptValue>(column.Name, column.AsValue()));

                    AppendObject(builder, fields, fields.Count, pretty, depth);
                    return;
                }

            default:
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"значение типа {value.Type.ToName()} не переводится в JSON",
                    "в JSON пишутся числа, строки, логические значения, списки, записи и таблицы");
        }
    }

    private static void AppendArray(StringBuilder builder, ScriptList list, bool pretty, int depth)
    {
        if (list.Count == 0)
        {
            _ = builder.Append("[]");
            return;
        }

        _ = builder.Append('[');

        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) _ = builder.Append(',');

            AppendBreak(builder, pretty, depth + 1);
            Append(builder, list[i], pretty, depth + 1);
        }

        AppendBreak(builder, pretty, depth);
        _ = builder.Append(']');
    }

    private static void AppendObject(
        StringBuilder builder,
        IEnumerable<KeyValuePair<string, ScriptValue>> fields,
        int count,
        bool pretty,
        int depth)
    {
        if (count == 0)
        {
            _ = builder.Append("{}");
            return;
        }

        _ = builder.Append('{');
        bool first = true;

        foreach (var field in fields)
        {
            if (!first) _ = builder.Append(',');
            first = false;

            AppendBreak(builder, pretty, depth + 1);
            AppendString(builder, field.Key);
            _ = builder.Append(pretty ? ": " : ":");
            Append(builder, field.Value, pretty, depth + 1);
        }

        AppendBreak(builder, pretty, depth);
        _ = builder.Append('}');
    }

    private static void AppendBreak(StringBuilder builder, bool pretty, int depth)
    {
        if (!pretty) return;

        _ = builder.Append('\n').Append(' ', depth * 2);
    }

    private static void AppendNumber(StringBuilder builder, double value)
    {
        // JSON не знает nan и inf: они становятся null, иначе получится файл, который не
        // прочитает ни один разборщик, включая наш собственный.
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            _ = builder.Append("null");
            return;
        }

        _ = builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void AppendString(StringBuilder builder, string text)
    {
        _ = builder.Append('"');

        foreach (char c in text)
        {
            switch (c)
            {
                case '"': _ = builder.Append("\\\""); break;
                case '\\': _ = builder.Append("\\\\"); break;
                case '\n': _ = builder.Append("\\n"); break;
                case '\r': _ = builder.Append("\\r"); break;
                case '\t': _ = builder.Append("\\t"); break;
                default:
                    if (c < ' ') _ = builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else _ = builder.Append(c);
                    break;
            }
        }

        _ = builder.Append('"');
    }
}
