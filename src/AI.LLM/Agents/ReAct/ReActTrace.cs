namespace AI.LLM.Agents.ReAct;

/// <summary>
/// След прогона: накопленные шаги и источники. Владелец состояния — движок, поэтому мутаторы
/// внутренние.
/// <para>
/// След намеренно хранится структурой, а не одной склеенной строкой. Строку неизбежно
/// начинают резать «по длине», и обрезка с начала выбрасывает как раз самые свежие наблюдения —
/// модель перестаёт видеть, что уже сделала, и повторяет действия. Здесь усечением занимается
/// отдельный компонент (<see cref="Rendering.IReActTraceRenderer"/>), у которого это единственная
/// задача и который можно проверить отдельно.
/// </para>
/// </summary>
public sealed class ReActTrace
{
    private readonly List<ReActStep> _steps = [];
    private readonly List<ReActCitation> _citations = [];
    private readonly HashSet<string> _citationUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReActObservation> _byActionKey = new(StringComparer.Ordinal);

    /// <summary>Шаги в порядке выполнения. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<ReActStep> Steps => _steps;

    /// <summary>Источники, накопленные за прогон, без повторов по адресу.</summary>
    public IReadOnlyList<ReActCitation> Citations => _citations;

    /// <summary>Число выполненных шагов.</summary>
    public int Count => _steps.Count;

    /// <summary>Ни одного действия за прогон ещё не выполнено.</summary>
    public bool IsEmpty => _steps.Count == 0;

    /// <summary>Добавляет шаг и накапливает его источники.</summary>
    /// <param name="step">Завершённый шаг.</param>
    internal void Add(ReActStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _steps.Add(step);

        foreach (ReActObservation observation in step.Observations)
        {
            foreach (ReActCitation citation in observation.Citations)
            {
                if (_citationUrls.Add(citation.Url))
                    _citations.Add(citation);
            }
        }
    }

    /// <summary>Запоминает наблюдение под ключом действия — для распознавания повторов.</summary>
    /// <param name="key">Канонизированный ключ действия.</param>
    /// <param name="observation">Наблюдение.</param>
    internal void Remember(string key, ReActObservation observation)
    {
        if (!string.IsNullOrEmpty(key))
            _byActionKey[key] = observation;
    }

    /// <summary>Ранее полученное наблюдение для того же действия; <c>null</c>, если такого не было.</summary>
    /// <param name="key">Канонизированный ключ действия.</param>
    internal ReActObservation Recall(string key) =>
        !string.IsNullOrEmpty(key) && _byActionKey.TryGetValue(key, out ReActObservation observation)
            ? observation
            : null;

    /// <summary>Сколько раз подряд в конце следа падал инструмент с указанным именем.</summary>
    /// <param name="toolName">Имя инструмента.</param>
    internal int TrailingFailures(string toolName)
    {
        int count = 0;
        for (int i = _steps.Count - 1; i >= 0; i--)
        {
            bool matched = false;
            foreach (ReActObservation observation in _steps[i].Observations)
            {
                if (observation.Action == null
                    || !string.Equals(observation.Action.ToolName, toolName, StringComparison.Ordinal))
                    continue;

                matched = true;
                if (observation.Ok)
                    return count;
            }

            if (!matched)
                return count;

            count++;
        }

        return count;
    }
}
