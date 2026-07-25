namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Один завершённый шаг цикла — полная триада ReAct: Рассуждение → Действия → Наблюдения.
/// <para>
/// Отдельный тип от <see cref="AgentStep"/>: там «наблюдение» означает снимок окружения
/// (изображения), а результаты инструментов лежат отдельным списком. Здесь наблюдение — это
/// именно то, что вернул инструмент, как того и требует определение ReAct.
/// </para>
/// </summary>
public sealed class ReActStep
{
    /// <summary>Номер шага, начиная с единицы.</summary>
    public int Number { get; init; }

    /// <summary>Рассуждение модели перед действиями. Может быть <c>null</c>.</summary>
    public string Thought { get; init; }

    /// <summary>Запрошенные действия. Никогда не <c>null</c>; пусто на шаге без действий.</summary>
    public IReadOnlyList<ReActAction> Actions { get; init; } = [];

    /// <summary>Наблюдения в порядке действий. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<ReActObservation> Observations { get; init; } = [];

    /// <summary>
    /// Служебная пометка, добавленная самим движком: повтор действия, неизвестный инструмент,
    /// неразобранный ответ. Показывается модели вместе с наблюдениями. Может быть <c>null</c>.
    /// </summary>
    public string Note { get; init; }

    /// <summary>Все наблюдения шага успешны.</summary>
    public bool Ok
    {
        get
        {
            foreach (ReActObservation observation in Observations)
            {
                if (!observation.Ok)
                    return false;
            }

            return true;
        }
    }
}
