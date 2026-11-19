namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Файл, который инструмент создал, изменил или вернул: документ, снимок экрана, картинка.
/// </summary>
/// <remarks>
/// Картинки для модели едут этим же типом: <see cref="ShowToModel"/> = <see langword="true"/> и тип
/// содержимого <c>image/*</c>. Содержимое передается либо байтами (<see cref="Content"/>), либо адресом
/// (<see cref="Uri"/>).
/// </remarks>
public sealed record ToolFile
{
    /// <summary>Тип содержимого (MIME), например <c>image/png</c> или <c>application/pdf</c>.</summary>
    public required string ContentType { get; init; }

    /// <summary>Имя файла для человека; <see langword="null"/>: имени нет (например, у снимка экрана).</summary>
    public string? Name { get; init; }

    /// <summary>Где файл лежит: путь, адрес, <c>data:</c>; <see langword="null"/>: содержимое только в <see cref="Content"/>.</summary>
    public string? Uri { get; init; }

    /// <summary>Содержимое байтами; <see langword="null"/>: содержимое по <see cref="Uri"/>.</summary>
    public byte[]? Content { get; init; }

    /// <summary>Показать файл модели в следующем шаге (картинка из инструмента); <see langword="false"/>: не показывать.</summary>
    public bool ShowToModel { get; init; }
}
