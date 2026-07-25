using System.Text;
using System.Text.Json;

namespace AI.LLM.Agents.ReAct.Tools;

/// <summary>
/// Канонизированный ключ действия «инструмент + аргумент». Нужен, чтобы распознать повтор
/// действия независимо от пробелов, регистра имени и порядка полей в JSON.
/// </summary>
/// <remarks>
/// Повтор ловится по ключу, а не по факту падения. Прежние реализации отслеживали только
/// повторные ОШИБКИ, и успешный, но бесполезный повтор того же вызова спокойно выедал весь
/// бюджет итераций.
/// </remarks>
internal static class ReActActionKey
{
    /// <summary>Строит ключ действия.</summary>
    /// <param name="toolName">Имя инструмента.</param>
    /// <param name="arguments">Аргумент: JSON либо простая строка.</param>
    public static string Create(string toolName, string arguments)
    {
        string name = (toolName ?? string.Empty).Trim().ToLowerInvariant();

        // Длина имени вместо символа-разделителя: так имя и аргумент не могут «перетечь»
        // друг в друга ни при каком содержимом, и не нужен служебный символ.
        var sb = new StringBuilder();
        sb.Append(name.Length).Append(':').Append(name).Append(':');
        sb.Append(Canonicalize(arguments));
        return sb.ToString();
    }

    private static string Canonicalize(string arguments)
    {
        string text = (arguments ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        if (text[0] is '{' or '[')
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                var sb = new StringBuilder();
                Write(document.RootElement, sb);
                return sb.ToString();
            }
            catch (JsonException)
            {
                // Не JSON — сравниваем как обычный текст.
            }
        }

        return CollapseWhitespace(text);
    }

    /// <summary>Пишет элемент с сортировкой полей — порядок полей не должен влиять на ключ.</summary>
    private static void Write(JsonElement element, StringBuilder sb)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append('{');
                var names = new List<string>();
                foreach (JsonProperty property in element.EnumerateObject())
                    names.Add(property.Name);
                names.Sort(StringComparer.Ordinal);
                foreach (string name in names)
                {
                    sb.Append(name).Append(':');
                    Write(element.GetProperty(name), sb);
                    sb.Append(',');
                }

                sb.Append('}');
                break;

            case JsonValueKind.Array:
                sb.Append('[');
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Write(item, sb);
                    sb.Append(',');
                }

                sb.Append(']');
                break;

            case JsonValueKind.String:
                sb.Append(CollapseWhitespace(element.GetString()));
                break;

            default:
                sb.Append(element.ToString());
                break;
        }
    }

    private static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space && sb.Length > 0)
                sb.Append(' ');

            space = false;
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
