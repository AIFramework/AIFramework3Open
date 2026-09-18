using AI.Charts.JS;
using AI.Script.Hosting;
using System.Text;

namespace AI.Script.Charts;

/// <summary>
/// График как значение: описание в формате Plotly плюс заголовок.
/// </summary>
/// <remarks>
/// Отдельный тип, а не строка JSON: <c>plot.grid</c> собирает несколько графиков в один
/// артефакт, а по строке уже не понять, где кончается один и начинается другой. Строка
/// получается по требованию — <see cref="ToJson"/>.
/// </remarks>
public sealed partial class PlotFigure : IScriptArtifactSource, IScriptImageSource
{
    private readonly PlotlyBuilder? _builder;
    private readonly IReadOnlyList<PlotFigure> _parts;
    private readonly IReadOnlyList<PlotSeries> _series = [];
    private readonly string _xlabel = string.Empty;
    private readonly string _ylabel = string.Empty;

    /// <summary>Заголовок графика.</summary>
    public string Title { get; }

    /// <summary>Составлен ли график из нескольких.</summary>
    public bool IsGrid => _builder == null;

    /// <summary>Части составного графика.</summary>
    public IReadOnlyList<PlotFigure> Parts => _parts;

    /// <summary>Создаёт одиночный график.</summary>
    /// <param name="title">Заголовок.</param>
    /// <param name="builder">Описание Plotly.</param>
    /// <param name="series">Те же серии числами — для картинки; пусто — график рисует только браузер.</param>
    /// <param name="xlabel">Подпись оси X.</param>
    /// <param name="ylabel">Подпись оси Y.</param>
    public PlotFigure(
        string title,
        PlotlyBuilder builder,
        IReadOnlyList<PlotSeries>? series = null,
        string xlabel = "",
        string ylabel = "")
    {
        Title = title;
        _builder = builder;
        _parts = [];
        _series = series ?? [];
        _xlabel = xlabel;
        _ylabel = ylabel;
    }

    /// <summary>Создаёт составной график.</summary>
    public PlotFigure(string title, IReadOnlyList<PlotFigure> parts)
    {
        Title = title;
        _builder = null;
        _parts = parts;
    }

    /// <summary>Описание графика в формате Plotly; для составного — массив описаний.</summary>
    public string ToJson()
    {
        if (_builder != null) return _builder.Build();

        var builder = new StringBuilder("[");

        for (int i = 0; i < _parts.Count; i++)
        {
            if (i > 0) _ = builder.Append(',');

            _ = builder.Append(_parts[i].ToJson());
        }

        return builder.Append(']').ToString();
    }

    /// <inheritdoc/>
    public string ArtifactKind => "plot";

    /// <inheritdoc/>
    public string ArtifactTitle => Title;

    /// <summary>
    /// Описание графика для хоста: строка Plotly-JSON.
    /// </summary>
    /// <remarks>
    /// Именно строка, а не объект: хост отдаёт её в JavaScript как есть, и промежуточная
    /// сериализация на его стороне была бы лишним местом, где формат может разъехаться.
    /// </remarks>
    public object? ArtifactPayload => ToJson();

    /// <inheritdoc/>
    public override string ToString() =>
        IsGrid
            ? $"<plot: набор из {_parts.Count} графиков{(Title.Length > 0 ? $", «{Title}»" : string.Empty)}>"
            : $"<plot{(Title.Length > 0 ? $": «{Title}»" : string.Empty)}>";
}
