namespace AI.LLM.Agents.ReAct.Synthesis;

/// <summary>
/// Пишет итоговый ответ по собранным наблюдениям — отдельным обращением к модели.
/// <para>
/// Отдельным, потому что канал принятия решений для этого не годится: он работает в урезанном
/// бюджете, часто в JSON-режиме и с нулевой температурой. Текст, рождённый в таком канале,
/// получается обрубленным и плоским.
/// </para>
/// </summary>
public interface IReActSynthesizer
{
    /// <summary>Пишет итоговый ответ.</summary>
    /// <param name="context">Наблюдения, черновик, источники и причина остановки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Фрагменты ответа; допускается один фрагмент целиком.</returns>
    IAsyncEnumerable<ReActTextChunk> SynthesizeAsync(
        ReActSynthesisContext context, CancellationToken cancellationToken = default);
}
