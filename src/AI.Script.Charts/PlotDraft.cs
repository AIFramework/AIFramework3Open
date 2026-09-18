using AI.Charts.JS;
using AI.Script.Runtime;

namespace AI.Script.Charts;

/// <summary>Серия графика в том виде, в каком её можно нарисовать без браузера.</summary>
/// <param name="Kind">Вид серии: <c>line</c>, <c>scatter</c>, <c>bar</c>.</param>
/// <param name="X">Значения по оси X.</param>
/// <param name="Y">Значения по оси Y.</param>
/// <param name="Name">Подпись серии; <c>null</c> — без подписи.</param>
public sealed record PlotSeries(string Kind, double[] X, double[] Y, string? Name);

/// <summary>
/// Черновик графика: описание Plotly и те же серии числами.
/// </summary>
/// <remarks>
/// Plotly рисует браузер, а отчёту в Word нужна картинка. Держать серии рядом с описанием дешевле,
/// чем разбирать собственный JSON обратно: одно место записи — и график, и его растровая копия
/// гарантированно показывают одно и то же.
/// </remarks>
internal sealed class PlotDraft(string title, string xlabel, string ylabel)
{
    private readonly List<PlotSeries> _series = [];

    /// <summary>Описание для браузера.</summary>
    public PlotlyBuilder Builder { get; } = new()
    {
        Title = string.IsNullOrWhiteSpace(title) ? null : title,
        AxisX = string.IsNullOrWhiteSpace(xlabel) ? null : xlabel,
        AxisY = string.IsNullOrWhiteSpace(ylabel) ? null : ylabel,
    };

    public void Line(double[] x, double[] y, string? name)
    {
        Builder.AddLine(x, y, name);
        _series.Add(new PlotSeries("line", x, y, name));
    }

    public void Scatter(double[] x, double[] y, string? name)
    {
        Builder.AddScatter2D(x, y, name);
        _series.Add(new PlotSeries("scatter", x, y, name));
    }

    public void Bar(double[] x, double[] y, string? name)
    {
        Builder.AddBar2D(x, y, name);
        _series.Add(new PlotSeries("bar", x, y, name));
    }

    /// <summary>Готовый график дескриптором языка.</summary>
    public ScriptHandle Handle(string summary) =>
        new(PlotModule.PlotHandle, new PlotFigure(title, Builder, _series, xlabel, ylabel), summary);
}
