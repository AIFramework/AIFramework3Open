using System.Runtime.CompilerServices;

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
    /// Тот же запрос, что <see cref="AskAsync(ModelAsk, CancellationToken)"/>, но ответ приходит потоком кадров.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Итог один: последний кадр потока всегда <see cref="LlmChunk.Final"/> с <see cref="LlmAnswer"/>, и после
    /// него кадров нет. Полный текст ответа берется из <see cref="LlmChunk.Final.Answer"/>; кадры
    /// <see cref="LlmChunk.Text"/> и <see cref="LlmChunk.Reasoning"/> только для показа. Сбой провайдера, как и
    /// в <see cref="AskAsync(ModelAsk, CancellationToken)"/>, приходит итогом со статусом
    /// <see cref="LlmStatus.Failed"/>; отмена токеном может прервать поток исключением
    /// <see cref="OperationCanceledException"/>.
    /// </para>
    /// <para>
    /// Реализация по умолчанию вызывает <see cref="AskAsync(ModelAsk, CancellationToken)"/> и отдает один кадр
    /// <see cref="LlmChunk.Final"/>, без имитации потока фрагментами. Порт, который умеет настоящий поток,
    /// переопределяет метод и сообщает это признаком <see cref="ModelCapabilities.Streaming"/>.
    /// </para>
    /// </remarks>
    /// <param name="ask">Запрос.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Поток кадров, последним — <see cref="LlmChunk.Final"/>.</returns>
    IAsyncEnumerable<LlmChunk> StreamAsync(ModelAsk ask, CancellationToken cancellationToken = default) =>
        SingleFinal(this, ask, cancellationToken);

    /// <summary>
    /// Что умеет модель, которую порт ставит на роль.
    /// </summary>
    /// <param name="role">Роль.</param>
    /// <returns>Возможности модели.</returns>
    ModelCapabilities Describe(ModelRole role);

    /// <summary>
    /// Поток из одного итогового кадра поверх <see cref="AskAsync(ModelAsk, CancellationToken)"/>. Итератор вынесен
    /// из метода интерфейса, чтобы в публичный API не попадало имя автомата состояний компилятора.
    /// </summary>
    private static async IAsyncEnumerable<LlmChunk> SingleFinal(
        IModelPort port, ModelAsk ask, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LlmAnswer answer = await port.AskAsync(ask, cancellationToken).ConfigureAwait(false);
        yield return new LlmChunk.Final { Answer = answer };
    }
}
