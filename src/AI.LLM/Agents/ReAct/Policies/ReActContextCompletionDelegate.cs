namespace AI.LLM.Agents.ReAct.Policies;

/// <summary>
/// Обращение к модели с контекстом шага: вызывающий видит набор инструментов ЭТОГО шага и может
/// закрыть схему ответа их именами. Набор меняется по ходу прогона (инструмент, отказавший
/// несколько раз подряд, блокируется), и схема, собранная один раз на прогон, отставала бы.
/// </summary>
/// <param name="context">Контекст шага: инструменты, след, номер шага.</param>
/// <param name="system">Системная инструкция.</param>
/// <param name="user">Запрос вместе с накопленными наблюдениями.</param>
/// <param name="cancellationToken">Токен отмены.</param>
/// <returns>Текст ответа модели.</returns>
public delegate Task<string> ReActContextCompletionDelegate(
    ReActPolicyContext context, string system, string user, CancellationToken cancellationToken);
