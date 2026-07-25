namespace AI.LLM.Agents.ReAct.Policies;

/// <summary>
/// Способ получить решение шага. Единственное место, где цикл соприкасается с языковой
/// моделью, — поэтому именно здесь и только здесь возникает зависимость от конкретного
/// клиента, а сам цикл остаётся независимым от поставщика.
/// </summary>
public interface IReActPolicy
{
    /// <summary>Принимает решение об одном шаге.</summary>
    /// <param name="context">Контекст шага.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>
    /// Решение шага. Реализация не бросает исключений при сбое модели и не выдаёт
    /// «завершено» вместо неразобранного ответа: для этого есть
    /// <see cref="ReActDecision.Malformed"/>, и только цикл решает, что с этим делать.
    /// </returns>
    Task<ReActDecision> DecideAsync(ReActPolicyContext context, CancellationToken cancellationToken = default);
}
