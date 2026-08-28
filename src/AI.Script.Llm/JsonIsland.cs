using AI.Script.Runtime;
using AI.Script.Std;

namespace AI.Script.Llm;

/// <summary>
/// Выделение объекта JSON из свободного текста ответа модели.
/// </summary>
/// <remarks>
/// Модель обрамляет ответ пояснениями и оградами ```json, даже когда просили этого не делать.
/// Требовать чистый JSON и отказывать на всём остальном — значит проигрывать каждый десятый
/// вызов на форматировании, а не на существе. Поэтому разбирается первый сбалансированный
/// объект в тексте, а всё вокруг него отбрасывается.
/// <para>
/// Скобки внутри строк не считаются: без этого ответ с текстом «см. {пример}» в поле обрывал
/// бы объект на середине.
/// </para>
/// </remarks>
public static class JsonIsland
{
    /// <summary>
    /// Пытается найти и разобрать объект JSON.
    /// </summary>
    /// <param name="text">Ответ модели.</param>
    /// <param name="value">Разобранное значение.</param>
    /// <returns><c>true</c>, если объект найден и разобран.</returns>
    public static bool TryExtract(string text, out ScriptValue value)
    {
        value = ScriptValue.None;

        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (Range span in Candidates(text))
        {
            try
            {
                value = Json.Parse(text[span], "llm.json");

                return true;
            }
            catch (ScriptError)
            {
                // Кандидат оказался не объектом JSON: пробуем следующий.
            }
        }

        return false;
    }

    /// <summary>Границы сбалансированных фрагментов, начинающихся с '{' или '['.</summary>
    private static IEnumerable<Range> Candidates(string text)
    {
        for (int start = 0; start < text.Length; start++)
        {
            char open = text[start];

            if (open is not ('{' or '[')) continue;

            char close = open == '{' ? '}' : ']';
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (inString)
                {
                    if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == open) depth++;
                else if (c == close && --depth == 0) yield return new Range(start, i + 1);
            }
        }
    }
}
