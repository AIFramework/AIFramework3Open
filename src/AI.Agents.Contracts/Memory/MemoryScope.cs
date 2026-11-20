namespace AI.Agents.Contracts.Memory;

/// <summary>
/// Чья память и где. Область ставит хост по сессии и правам, а не модель: модель не может записать в чужую
/// память или прочитать ее.
/// </summary>
public sealed record MemoryScope
{
    /// <summary>Владелец памяти: пользователь или организация (непрозрачный идентификатор хоста).</summary>
    public required string Owner { get; init; }

    /// <summary>Проект владельца; <see langword="null"/>: вне проекта.</summary>
    public string? Project { get; init; }

    /// <summary>Чат; <see langword="null"/>: не в чате (экран памяти, импорт, фоновая работа).</summary>
    public string? Chat { get; init; }
}
