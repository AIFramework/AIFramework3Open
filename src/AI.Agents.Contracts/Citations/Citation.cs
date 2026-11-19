namespace AI.Agents.Contracts.Citations;

/// <summary>
/// Подтверждение утверждения: на что опирается фраза ответа. Лежит рядом с текстом отдельной структурой,
/// а не пометкой внутри него, и годится для любой опоры — документа, страницы, инструмента, расчета, памяти,
/// внешнего сервиса.
/// </summary>
/// <remarks>
/// <para>
/// Точная привязка к источнику — дословная цитата и несколько знаков вокруг нее (<see cref="Prefix"/>,
/// <see cref="Suffix"/>; так устроен TextQuoteSelector модели веб-аннотаций W3C). Она переживает правку
/// источника и не зависит от его формата. Место в источнике (вложенный объект, страница, строки, время,
/// область) собрано в одну запись <see cref="Location"/> и помогает быстро открыть нужный фрагмент.
/// </para>
/// <para>
/// <see cref="At"/>, <see cref="Start"/> и <see cref="End"/> — позиции в тексте <b>ответа</b>, а не в источнике,
/// поэтому в <see cref="Location"/> они не входят. Инструмент отдает подтверждение раньше, чем появится ответ,
/// поэтому <see cref="Claim"/> и позиции в ответе необязательны.
/// </para>
/// </remarks>
public sealed record Citation
{
    /// <summary>Вид опоры.</summary>
    public required CitationKind Kind { get; init; }

    /// <summary>Короткое название опоры для показа: файл, страница, инструмент, сервис.</summary>
    public required string Title { get; init; }

    /// <summary>Дословный фрагмент опоры: цитата, вывод инструмента, формула.</summary>
    public required string Quote { get; init; }

    /// <summary>Адрес, по которому опору можно открыть; <see langword="null"/>: открывать нечего.</summary>
    public string? Url { get; init; }

    /// <summary>Фраза ответа, которую подтверждает опора; <see langword="null"/>: с ответом еще не сопоставлена.</summary>
    public string? Claim { get; init; }

    /// <summary>
    /// Опора проверена: цитата найдена в источнике, вызов инструмента удался. <see langword="false"/>: не
    /// проверена; так приходят подтверждения от инструментов и движков, проверяет их приемка.
    /// </summary>
    public bool Verified { get; init; }

    /// <summary>Знаки источника прямо перед цитатой, чтобы различать одинаковые цитаты; <see langword="null"/>: не заданы.</summary>
    public string? Prefix { get; init; }

    /// <summary>Знаки источника сразу после цитаты; <see langword="null"/>: не заданы.</summary>
    public string? Suffix { get; init; }

    /// <summary>
    /// Место цитаты в источнике: путь к вложенному объекту, страница, строки, время, область;
    /// <see langword="null"/>: место не указано, фрагмент ищется по цитате.
    /// </summary>
    public SourceLocation? Location { get; init; }

    /// <summary>Позиция сноски в тексте ответа; <see langword="null"/>: сноска еще не поставлена.</summary>
    public int? At { get; init; }

    /// <summary>Начало подтверждаемой фразы в тексте ответа; <see langword="null"/>: не определено.</summary>
    public int? Start { get; init; }

    /// <summary>Конец подтверждаемой фразы в тексте ответа, не включая; <see langword="null"/>: не определен.</summary>
    public int? End { get; init; }

    /// <summary>Исходная веб-страница; <see langword="null"/>: опора не со страницы.</summary>
    public string? Site { get; init; }

    /// <summary>Номер шага запуска, на котором получена опора; <see langword="null"/>: опора не из шага.</summary>
    public int? Step { get; init; }

    /// <summary>Момент получения опоры; <see langword="null"/>: неизвестен.</summary>
    public DateTimeOffset? Taken { get; init; }

    /// <summary>Исходные величины расчета, каждая со своей опорой; <see langword="null"/>: не расчет.</summary>
    public IReadOnlyList<Citation>? Basis { get; init; }
}
