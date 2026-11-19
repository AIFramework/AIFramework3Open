using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Citations;

/// <summary>
/// Сегмент пути <see cref="SourceLocation.Path"/>: вложенный объект одного вида, найденный по номеру,
/// по идентификатору или по обоим.
/// </summary>
/// <remarks>
/// Нужно хотя бы одно из <see cref="Index"/> и <see cref="Id"/>: сегмент без адреса никуда не ведет.
/// Отсутствие обоих отвергается при чтении JSON и при записи сегмента в <see cref="SourceLocation.Path"/>;
/// заданные значения проверяются сразу при создании.
/// </remarks>
public sealed record LocationSegment : IJsonOnDeserialized
{
    private readonly string _kind = "";
    private readonly int? _index;
    private readonly string? _id;

    /// <summary>
    /// Вид объекта. Набор открытый; известные значения собраны в <see cref="Kinds"/>, свой вид пишется
    /// строкой. Сравнение с учетом регистра.
    /// </summary>
    /// <exception cref="ArgumentException">Строка пустая или из пробелов.</exception>
    public required string Kind
    {
        get => _kind;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Kind));
            _kind = value;
        }
    }

    /// <summary>
    /// Номер объекта среди соседей того же вида, с 1, как его видит человек («слайд 3»);
    /// <see langword="null"/>: объект задан только <see cref="Id"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Номер меньше 1.</exception>
    public int? Index
    {
        get => _index;
        init => _index = value is null or >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Index), value, "Номер сегмента считается с 1, как его видит человек.");
    }

    /// <summary>
    /// Непрозрачный идентификатор объекта в источнике (имя листа, идентификатор фигуры, имя вложения);
    /// <see langword="null"/>: объект задан только <see cref="Index"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Строка пустая или из пробелов.</exception>
    public string? Id
    {
        get => _id;
        init => _id = value is null || !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Идентификатор сегмента не может быть пустым; без идентификатора — null.", nameof(Id));
    }

    /// <summary>Известные виды сегментов. Набор открытый: другой вид пишется своей строкой.</summary>
    public static class Kinds
    {
        /// <summary>Слайд презентации.</summary>
        public const string Slide = "slide";

        /// <summary>Картинка.</summary>
        public const string Image = "image";

        /// <summary>Лист книги.</summary>
        public const string Sheet = "sheet";

        /// <summary>Таблица.</summary>
        public const string Table = "table";

        /// <summary>Страница.</summary>
        public const string Page = "page";

        /// <summary>Вложение письма или контейнера.</summary>
        public const string Attachment = "attachment";
    }

    /// <inheritdoc />
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (_index is null && _id is null)
            throw new JsonException(MissingAddress(_kind));
    }

    /// <summary>Проверка адреса сегмента, когда он входит в путь.</summary>
    internal static void EnsureAddressed(LocationSegment? segment, string paramName)
    {
        if (segment is null)
            throw new ArgumentException("Сегмент пути не может быть null.", paramName);

        if (segment.Index is null && segment.Id is null)
            throw new ArgumentException(MissingAddress(segment.Kind), paramName);
    }

    private static string MissingAddress(string kind) =>
        $"Сегмент пути «{kind}»: нужен номер (Index) или идентификатор (Id), хотя бы одно из двух.";
}
