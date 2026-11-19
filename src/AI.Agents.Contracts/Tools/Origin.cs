namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Происхождение текста: доверенный или внешний, и откуда он.
/// </summary>
/// <remarks>
/// Сборщик контекста обрамляет текст с <see cref="OriginKind.External"/> как данные, а не как указание.
/// Поле структурное, а не метка в тексте: метку внешний текст может подделать.
/// </remarks>
public sealed record Origin
{
    /// <summary>Вид происхождения.</summary>
    public required OriginKind Kind { get; init; }

    /// <summary>Адрес источника (страница, сервер, ящик); <see langword="null"/>: адреса нет.</summary>
    public string? Address { get; init; }
}
