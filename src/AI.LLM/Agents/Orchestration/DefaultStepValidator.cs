using AI.LLM.Agents.Planning;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Приёмка шага без обращения к модели: шаг считается проваленным, если агент вернул пустоту
/// либо отчёт об отказе.
/// </summary>
/// <remarks>
/// Проверить <see cref="PlanStep.DoneWhen"/> здесь нечем: критерий готовности написан на
/// естественном языке, и сверить с ним результат может только модель — для этого есть
/// <see cref="LlmStepValidator"/>. Этот валидатор ловит лишь очевидное и намеренно склонен
/// принимать: ложный провал стоит дороже пропущенного, он гонит шаг на повтор, а затем на
/// перепланирование всей задачи.
/// <para>
/// Маркер отказа засчитывается, только если ответ ПОХОЖ на отчёт об отказе — короткий целиком
/// либо начинающийся с отказа. Прежняя версия искала маркеры по всему тексту, и «cannot» в
/// середине готового эссе объявляло провалом сделанную работу.
/// </para>
/// </remarks>
public sealed class DefaultStepValidator : IStepValidator
{
    /// <summary>Признаки отчёта об отказе.</summary>
    private static readonly string[] DefaultFailureMarkers =
    [
        "error:", "ошибка:", "failed", "не удалось", "timeout", "not found", "blocked",
        "exception", "unable to", "cannot", "could not"
    ];

    private readonly string[] _markers;
    private readonly int _reportMaxChars;

    /// <param name="failureReportMaxChars">
    /// До какой длины ответ считается отчётом, а не результатом работы. Ответ короче проверяется
    /// на маркеры целиком, длиннее — только по первой строке.
    /// </param>
    /// <param name="failureMarkers">
    /// Свои признаки отказа вместо стандартных. Регистр не важен. <c>null</c> — стандартные.
    /// </param>
    public DefaultStepValidator(int failureReportMaxChars = 400, IEnumerable<string> failureMarkers = null)
    {
        _reportMaxChars = Math.Max(1, failureReportMaxChars);
        _markers = failureMarkers is null
            ? DefaultFailureMarkers
            : [.. failureMarkers.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.ToLowerInvariant())];
    }

    /// <inheritdoc/>
    public bool IsSuccess(PlanStep step, AgentResult result)
    {
        if (result is null)
            return false;

        var answer = result.Answer?.Trim();
        if (string.IsNullOrEmpty(answer))
            return false;

        // Короткий ответ — это и есть отчёт: маркер в нём означает отказ.
        if (answer.Length <= _reportMaxChars)
            return !ContainsMarker(answer);

        // Длинный ответ — это результат работы. Отказ в таком случае стоит в начале
        // («Не удалось …, потому что …»), а маркер на пятой странице текста к приёмке
        // отношения не имеет.
        return !ContainsMarker(Head(answer));
    }

    private bool ContainsMarker(string text)
    {
        var lower = text.ToLowerInvariant();
        foreach (var marker in _markers)
        {
            if (lower.Contains(marker))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Начало ответа: первая строка, но не длиннее порога отчёта.
    /// </summary>
    /// <remarks>
    /// Ограничение по длине обязательно: сплошной текст без переносов — одна «первая строка»
    /// на весь ответ, и проверка снова пошла бы по всему тексту.
    /// </remarks>
    private string Head(string text)
    {
        var end = text.IndexOf('\n');
        var length = end < 0 ? text.Length : end;
        return text[..Math.Min(length, _reportMaxChars)];
    }
}
