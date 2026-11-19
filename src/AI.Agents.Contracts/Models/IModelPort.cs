namespace AI.Agents.Contracts.Models;

/// <summary>
/// Единственный путь движка к модели. Роль сопоставляет с моделью реализация; повторы, запасная модель,
/// плательщик и фильтр секретов тоже живут в реализации.
/// </summary>
public interface IModelPort
{
    /// <summary>
    /// Запрос к модели: текст, ответ по схеме, вызовы инструментов.
    /// </summary>
    /// <remarks>
    /// Сбой провайдера приходит итогом со статусом <see cref="LlmStatus.Failed"/> и кодом, а не исключением.
    /// Отмена токеном — исключение <see cref="OperationCanceledException"/>.
    /// </remarks>
    /// <param name="ask">Запрос.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Итог вызова.</returns>
    Task<LlmAnswer> AskAsync(ModelAsk ask, CancellationToken cancellationToken = default);

    /// <summary>
    /// Что умеет модель, которую порт ставит на роль.
    /// </summary>
    /// <param name="role">Роль.</param>
    /// <returns>Возможности модели.</returns>
    ModelCapabilities Describe(ModelRole role);
}
