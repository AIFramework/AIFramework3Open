namespace AI.Agents.Contracts.Citations;

/// <summary>
/// Место внутри источника: путь к вложенному объекту, страница, строки, отрезок времени и область.
/// Все части необязательны и задаются по мере того, что известно об опоре.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Path"/> ведет от самого документа к вложенному объекту: «слайд 3» → «картинка 2». Страница,
/// строки, время и прямоугольник относятся к <b>последнему</b> сегменту пути; если пути нет — к самому
/// документу. Так «строки 10–12» у пути «вложение 1» означают строки вложения, а не письма.
/// </para>
/// <para>
/// Место дополняет дословную цитату <see cref="Citation.Quote"/>, а не заменяет ее: цитата находит
/// фрагмент и после правки источника, место быстро открывает нужный слайд, страницу или секунду записи.
/// </para>
/// </remarks>
public sealed record SourceLocation
{
    private readonly IReadOnlyList<LocationSegment>? _path;
    private readonly int? _page;

    /// <summary>
    /// Путь от документа к вложенному объекту, от внешнего к внутреннему; <see langword="null"/>: место
    /// в самом документе. Пустой список означает то же, что <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentException">У сегмента нет ни <see cref="LocationSegment.Index"/>, ни <see cref="LocationSegment.Id"/>, или сегмент равен null.</exception>
    public IReadOnlyList<LocationSegment>? Path
    {
        get => _path;
        init
        {
            if (value is not null)
            {
                for (int i = 0; i < value.Count; i++)
                    LocationSegment.EnsureAddressed(value[i], $"{nameof(Path)}[{i}]");
            }

            _path = value;
        }
    }

    /// <summary>Номер страницы последнего сегмента пути (или документа), с 1; <see langword="null"/>: не указан.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Номер меньше 1.</exception>
    public int? Page
    {
        get => _page;
        init => _page = value is null or >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Page), value, "Номер страницы считается с 1.");
    }

    /// <summary>Строки последнего сегмента пути (или документа); <see langword="null"/>: не указаны.</summary>
    public LineRange? Lines { get; init; }

    /// <summary>Отрезок записи (звук, видео) последнего сегмента пути (или документа); <see langword="null"/>: не указан.</summary>
    public TimeInterval? Time { get; init; }

    /// <summary>Область на последнем сегменте пути (слайде, картинке, странице) в долях; <see langword="null"/>: не указана.</summary>
    public RelativeRect? Rect { get; init; }
}
