using System.Text;
using System.Text.Json;

namespace AI.LLM.Agents.ReAct.Policies;

/// <summary>
/// Извлечение JSON из ответа модели. Модели регулярно оборачивают ответ в тройные кавычки,
/// добавляют пояснения до и после и вкладывают JSON в текст, поэтому строгий разбор
/// <c>JsonDocument.Parse(raw)</c> отбраковывает вполне пригодные ответы.
/// </summary>
internal static class ReActJsonParser
{
    /// <summary>Убирает обрамление вида ```json … ``` и лишние пробелы.</summary>
    /// <param name="text">Сырой ответ модели.</param>
    public static string StripCodeFences(string text)
    {
        string value = (text ?? string.Empty).Trim();
        if (!value.StartsWith("```", StringComparison.Ordinal))
            return value;

        int firstNewLine = value.IndexOf('\n');
        if (firstNewLine < 0)
            return value;

        value = value[(firstNewLine + 1)..];

        int closing = value.LastIndexOf("```", StringComparison.Ordinal);
        if (closing >= 0)
            value = value[..closing];

        return value.Trim();
    }

    /// <summary>
    /// Возвращает первый сбалансированный JSON-объект из текста либо <c>null</c>.
    /// Учитывает строки и экранирование, поэтому фигурная скобка внутри строкового значения
    /// не сбивает счётчик вложенности.
    /// </summary>
    /// <param name="text">Текст, возможно содержащий JSON.</param>
    public static string ExtractObject(string text)
    {
        string value = StripCodeFences(text);
        if (value.Length == 0)
            return null;

        for (int start = 0; start < value.Length; start++)
        {
            if (value[start] != '{')
                continue;

            int depth = 0;
            bool inString = false;

            for (int i = start; i < value.Length; i++)
            {
                char c = value[i];

                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }

                    if (c == '"')
                        inString = false;

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        break;
                    case '{':
                        depth++;
                        break;
                    case '}':
                        depth--;
                        if (depth == 0)
                            return value[start..(i + 1)];
                        break;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Разбирает текст в JSON-объект; <c>null</c>, если ничего пригодного нет.
    /// Несбалансированный объект (обрезка ответа по лимиту токенов) пробует достроить —
    /// см. <see cref="RepairTruncatedObject"/>.
    /// </summary>
    /// <param name="text">Сырой ответ модели.</param>
    public static JsonDocument TryParseObject(string text)
    {
        string json = ExtractObject(text) ?? RepairTruncatedObject(text);
        if (json == null)
            return null;

        try
        {
            JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                return document;

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Достраивает JSON-объект, обрезанный на середине (лимит токенов): закрывает
    /// незавершённую строку и незакрытые скобки. Значение оборванного поля при этом
    /// остаётся неполным — но уцелевшие поля лучше потерянного шага целиком.
    /// </summary>
    /// <param name="text">Сырой ответ модели.</param>
    /// <returns>Валидно закрытый объект либо <c>null</c>, если чинить нечего или не вышло.</returns>
    public static string RepairTruncatedObject(string text)
    {
        string value = StripCodeFences(text);
        int start = value.IndexOf('{');
        if (start < 0)
            return null;

        var closers = new Stack<char>();
        bool inString = false;

        for (int i = start; i < value.Length; i++)
        {
            char c = value[i];

            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '"')
                    inString = false;

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    closers.Push('}');
                    break;
                case '[':
                    closers.Push(']');
                    break;
                case '}':
                case ']':
                    // Сломанная вложенность — это не обрезка, такое не чиним.
                    if (closers.Count == 0 || closers.Peek() != c)
                        return null;

                    closers.Pop();
                    break;
            }
        }

        // Сбалансированный объект нашёл бы ExtractObject — здесь делать нечего.
        if (closers.Count == 0)
            return null;

        string body = value[start..].TrimEnd();

        // Оборванный экран в конце строки ломает и достроенную версию.
        if (inString && body.EndsWith("\\", StringComparison.Ordinal))
            body = body[..^1];

        if (inString)
            body += "\"";

        body = body.TrimEnd();

        // Обрыв сразу после запятой или двоеточия: голый хвост не даст распарситься.
        if (body.EndsWith(",", StringComparison.Ordinal))
            body = body[..^1];
        else if (body.EndsWith(":", StringComparison.Ordinal))
            body += "null";

        var sb = new StringBuilder(body);
        while (closers.Count > 0)
            sb.Append(closers.Pop());

        return sb.ToString();
    }

    /// <summary>
    /// Ищет сигнал завершения среди перечисленных полей.
    /// </summary>
    /// <param name="root">Корневой объект.</param>
    /// <param name="names">Имена полей завершения.</param>
    /// <param name="text">Текст ответа, если он строкой; иначе <c>null</c>.</param>
    /// <returns><c>true</c>, если модель сигналит о завершении.</returns>
    /// <remarks>
    /// Значение может быть не строкой: <c>{"final":true}</c> — это «данных достаточно», а не
    /// ответ «True». Приводить такое к тексту нельзя, иначе слово-заглушка уедет в итоговый
    /// ответ. При этом <c>{"final":false}</c> завершением не считается.
    /// </remarks>
    public static bool TryGetFinal(JsonElement root, string[] names, out string text)
    {
        text = null;

        foreach (string name in names)
        {
            if (!root.TryGetProperty(name, out JsonElement value))
                continue;

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    string s = value.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        text = s;
                    return true;

                case JsonValueKind.False:
                case JsonValueKind.Null:
                    continue;

                default:
                    return true;
            }
        }

        return false;
    }

    /// <summary>Первое непустое строковое поле из перечисленных; <c>null</c>, если ни одного нет.</summary>
    /// <param name="root">Корневой объект.</param>
    /// <param name="names">Имена полей в порядке предпочтения.</param>
    public static string FirstString(JsonElement root, params string[] names)
    {
        foreach (string name in names)
        {
            if (!root.TryGetProperty(name, out JsonElement value))
                continue;

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    string text = value.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                    break;

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    return value.GetRawText();

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return value.ToString();
            }
        }

        return null;
    }
}
