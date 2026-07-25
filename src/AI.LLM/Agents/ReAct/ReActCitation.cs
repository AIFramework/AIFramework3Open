namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Источник, на который опирается наблюдение (веб-страница, документ, запись базы).
/// Движок только накапливает и дедуплицирует источники по адресу; их оформление —
/// дело вызывающей стороны.
/// </summary>
public sealed class ReActCitation
{
    /// <summary>Адрес источника. Никогда не пуст.</summary>
    public string Url { get; }

    /// <summary>Заголовок источника. Если он неизвестен, повторяет адрес.</summary>
    public string Title { get; }

    /// <summary>Фрагмент текста источника. Может быть <c>null</c>.</summary>
    public string Snippet { get; }

    /// <summary>Создаёт ссылку на источник.</summary>
    /// <param name="url">Адрес источника; обязателен.</param>
    /// <param name="title">Заголовок; при отсутствии подставляется адрес.</param>
    /// <param name="snippet">Фрагмент текста; необязателен.</param>
    public ReActCitation(string url, string title = null, string snippet = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Адрес источника не может быть пустым.", nameof(url));

        Url = url.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? Url : title.Trim();
        Snippet = snippet;
    }
}
