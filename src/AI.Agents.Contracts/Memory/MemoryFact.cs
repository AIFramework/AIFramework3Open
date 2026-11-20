namespace AI.Agents.Contracts.Memory;

/// <summary>
/// Факт — свойство объекта памяти: у <see cref="Subject"/> свойство <see cref="Property"/> имеет значение
/// <see cref="Value"/>. С происхождением, временем мира, временем записи и опорами.
/// </summary>
/// <remarks>
/// Устаревший факт не удаляется, а закрывается сроком <see cref="ValidTo"/>: история свойства — цепочка
/// закрытых фактов. Живой факт — тот, у которого <see cref="ValidTo"/> пуст или в будущем.
/// </remarks>
public sealed record MemoryFact
{
    /// <summary>
    /// Идентификатор факта в памяти; <see langword="null"/>: факт еще не записан (его предлагает запись, а
    /// идентификатор назначит реализация). В срезе памяти идентификатор есть всегда.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>Объект, чье это свойство.</summary>
    public required MemoryObject Subject { get; init; }

    /// <summary>Свойство: идентификатор связи из онтологии памяти.</summary>
    public required string Property { get; init; }

    /// <summary>Значение словами.</summary>
    public required string Value { get; init; }

    /// <summary>
    /// Значение-объект, если свойство связывает два объекта («работает в» → компания);
    /// <see langword="null"/>: значение не объект.
    /// </summary>
    public MemoryObject? Target { get; init; }

    /// <summary>Происхождение значения.</summary>
    public required ValueSource Source { get; init; }

    /// <summary>Время мира: с какого дня значение верно; <see langword="null"/>: неизвестно.</summary>
    public DateOnly? ValidFrom { get; init; }

    /// <summary>Время мира: по какой день значение было верно; <see langword="null"/>: верно сейчас.</summary>
    public DateOnly? ValidTo { get; init; }

    /// <summary>Время записи в память; <see langword="null"/>: факт еще не записан.</summary>
    public DateTimeOffset? Recorded { get; init; }

    /// <summary>
    /// Эпизоды-опоры (<see cref="MemoryEpisode.Id"/>); факт без опор закрывается сроком;
    /// <see langword="null"/>: опоры не переданы.
    /// </summary>
    public IReadOnlyList<string>? Episodes { get; init; }

    /// <summary>
    /// Посылки вывода — идентификаторы фактов, из которых выведено значение (для <see cref="ValueSource.Derived"/>);
    /// <see langword="null"/>: не вывод.
    /// </summary>
    public IReadOnlyList<string>? Premises { get; init; }

    /// <summary>
    /// Дословная цитата, на которой держится сказанное (для <see cref="ValueSource.Said"/>);
    /// <see langword="null"/>: цитаты нет.
    /// </summary>
    public string? Quote { get; init; }
}
