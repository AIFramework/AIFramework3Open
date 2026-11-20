namespace AI.Agents.Contracts.Memory;

/// <summary>
/// Порт памяти: записать эпизод (или явные изменения) и показать память под задачу. Реализацию дает продукт,
/// фреймворк содержит только форму.
/// </summary>
/// <remarks>
/// <para>
/// Через этот порт к памяти ходят все: ход агента, правка человеком, решение по предложению, импорт и RAG
/// (последний — через <see cref="ViewAsync"/> с малым бюджетом). Второго входа для записи нет: каждый путь
/// проходит одни операции (<see cref="MemoryOp"/>) и одни проверки реализации.
/// </para>
/// <para>
/// Сбой приходит статусом (<see cref="MemoryStatus"/>) с причиной, а не исключением и не пустым ответом.
/// Исключение допустимо только при отмене через <see cref="CancellationToken"/>.
/// </para>
/// </remarks>
public interface IMemoryPort
{
    /// <summary>
    /// Записать эпизод: извлечь из него факты или применить явные изменения, снять эпизоды. У каждого изменения
    /// свой исход с причиной.
    /// </summary>
    /// <param name="write">Запись: область, эпизод, изменения, снятые эпизоды.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Статус и исход каждого изменения.</returns>
    Task<MemoryWriteResult> WriteAsync(MemoryWrite write, CancellationToken cancellationToken = default);

    /// <summary>
    /// Память под задачу в пределах бюджета: профиль, фокус по задаче, имена остальных объектов и след отбора.
    /// </summary>
    /// <param name="ask">Запрос: область, задача, бюджет.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <returns>Срез памяти со статусом; бюджет не растет вместе с памятью.</returns>
    Task<MemoryView> ViewAsync(MemoryAsk ask, CancellationToken cancellationToken = default);
}
