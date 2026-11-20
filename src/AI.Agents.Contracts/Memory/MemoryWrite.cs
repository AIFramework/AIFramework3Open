namespace AI.Agents.Contracts.Memory;

/// <summary>
/// Запись в память: эпизод и либо явные изменения, либо просьба извлечь факты из текста эпизода.
/// </summary>
/// <remarks>
/// Один вход для всех путей записи: ход, правка человеком, одобрение предложения, дизлайк, импорт. Явные
/// изменения проходят те же проверки, что и извлеченные (область, дубль, потолок, время).
/// </remarks>
public sealed record MemoryWrite
{
    /// <summary>Область записи; ставит хост.</summary>
    public required MemoryScope Scope { get; init; }

    /// <summary>Эпизод, на котором держатся изменения.</summary>
    public required MemoryEpisode Episode { get; init; }

    /// <summary>
    /// Явные изменения в порядке применения; <see langword="null"/>: изменений нет, реализация извлекает факты
    /// из <see cref="MemoryEpisode.Text"/> сама.
    /// </summary>
    public IReadOnlyList<MemoryChange>? Changes { get; init; }

    /// <summary>
    /// Идентификаторы снятых эпизодов: удалено сообщение, «забыть чат». Опоры этих эпизодов снимаются, факт без
    /// опор закрывается сроком; <see langword="null"/>: ничего не снято.
    /// </summary>
    public IReadOnlyList<string>? Withdraw { get; init; }
}
