using AI.Charts.JS;
using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Charts;

/// <summary>
/// Пространство <c>plot</c>: графики как артефакты прогона.
/// </summary>
/// <remarks>
/// Модуль ничего не рисует — он строит описание графика в формате Plotly. Рисует хост:
/// Blazor-демонстратор, тетрадь, веб-страница. Так один и тот же скрипт даёт картинку и в
/// браузере, и в отчёте, и в консоли (где остаётся текстовая строка), а модуль не тянет за
/// собой ни одного графического движка.
/// </remarks>
[ScriptModule("plot", "Графики: линии, точки, столбцы, гистограммы, тепловые карты", Version = "0.1")]
public static class PlotModule
{
    /// <summary>Тип-тег дескриптора графика.</summary>
    public const string PlotHandle = "plot.figure";

    /// <summary>Вид артефакта, под которым график попадает в результат прогона.</summary>
    public const string ArtifactKind = "plot";

    /// <summary>
    /// Линейный график.
    /// </summary>
    /// <remarks>
    /// Первым идёт ряд значений, а сетка по оси X — необязательна: чаще всего рисуют «вот
    /// эти числа», а не пару массивов. Обратный порядок заставлял бы придумывать индексы
    /// вручную в самом частом случае.
    /// </remarks>
    [ScriptFn("line", "Линейный график", Returns = PlotHandle,
        Example = "show plot.line(signal, x: t, title: \"Сигнал\")")]
    public static ScriptHandle Line(
        [ScriptParam("ряд значений")] Vector y,
        [ScriptParam("сетка по оси X; пусто — индексы")] Vector? x = null,
        [ScriptParam("заголовок")] string title = "",
        [ScriptParam("подпись серии")] string name = "",
        [ScriptParam("подпись оси X")] string xlabel = "",
        [ScriptParam("подпись оси Y")] string ylabel = "")
    {
        (Vector abscissa, Vector ordinate) = Pair(y, x, "plot.line");

        var builder = Builder(title, xlabel, ylabel);
        builder.AddLine(abscissa.ToArray(), ordinate.ToArray(), Name(name));

        return Figure(builder, title, $"линия, точек: {ordinate.Count}");
    }

    [ScriptFn("scatter", "Диаграмма рассеяния", Returns = PlotHandle,
        Example = "show plot.scatter(x: x[:, 0], y: x[:, 1], title: \"Кластеры\")")]
    public static ScriptHandle Scatter(
        [ScriptParam("значения по оси X")] Vector x,
        [ScriptParam("значения по оси Y")] Vector y,
        [ScriptParam("заголовок")] string title = "",
        [ScriptParam("подпись серии")] string name = "",
        [ScriptParam("подпись оси X")] string xlabel = "",
        [ScriptParam("подпись оси Y")] string ylabel = "")
    {
        RequireSameLength(x, y, "plot.scatter");

        var builder = Builder(title, xlabel, ylabel);
        builder.AddScatter2D(x.ToArray(), y.ToArray(), Name(name));

        return Figure(builder, title, $"рассеяние, точек: {x.Count}");
    }

    /// <summary>
    /// Диаграмма рассеяния с разбиением на серии по меткам.
    /// </summary>
    /// <remarks>
    /// Отдельная функция, а не аргумент <c>color</c> у <see cref="Scatter"/>: точки разных
    /// классов должны попасть в разные серии, иначе легенда не покажет, где какой класс, —
    /// а именно ради этого на кластеризацию и смотрят.
    /// </remarks>
    [ScriptFn("scatter_by", "Рассеяние с разбиением на серии по меткам", Returns = PlotHandle,
        Example = "show plot.scatter_by(x: a, y: b, labels: clusters)")]
    public static ScriptHandle ScatterBy(
        [ScriptParam("значения по оси X")] Vector x,
        [ScriptParam("значения по оси Y")] Vector y,
        [ScriptParam("метки серий")] Vector labels,
        [ScriptParam("заголовок")] string title = "",
        [ScriptParam("подпись оси X")] string xlabel = "",
        [ScriptParam("подпись оси Y")] string ylabel = "")
    {
        RequireSameLength(x, y, "plot.scatter_by");
        RequireSameLength(x, labels, "plot.scatter_by");

        var groups = new Dictionary<double, List<int>>();
        var order = new List<double>();

        for (int i = 0; i < labels.Count; i++)
        {
            if (!groups.TryGetValue(labels[i], out List<int>? rows))
            {
                rows = [];
                groups[labels[i]] = rows;
                order.Add(labels[i]);
            }

            rows.Add(i);
        }

        order.Sort();

        var builder = Builder(title, xlabel, ylabel);

        foreach (double label in order)
        {
            List<int> rows = groups[label];
            var seriesX = new double[rows.Count];
            var seriesY = new double[rows.Count];

            for (int i = 0; i < rows.Count; i++)
            {
                seriesX[i] = x[rows[i]];
                seriesY[i] = y[rows[i]];
            }

            builder.AddScatter2D(seriesX, seriesY, ScriptFormatter.Number(label));
        }

        return Figure(builder, title, $"рассеяние, серий: {order.Count}");
    }

    [ScriptFn("bar", "Столбчатая диаграмма", Returns = PlotHandle,
        Example = "show plot.bar(<10, 20, 15>)")]
    public static ScriptHandle Bar(
        [ScriptParam("ряд значений")] Vector y,
        [ScriptParam("сетка по оси X; пусто — индексы")] Vector? x = null,
        [ScriptParam("заголовок")] string title = "",
        [ScriptParam("подпись серии")] string name = "",
        [ScriptParam("подпись оси X")] string xlabel = "",
        [ScriptParam("подпись оси Y")] string ylabel = "")
    {
        (Vector abscissa, Vector ordinate) = Pair(y, x, "plot.bar");

        var builder = Builder(title, xlabel, ylabel);
        builder.AddBar2D(abscissa.ToArray(), ordinate.ToArray(), Name(name));

        return Figure(builder, title, $"столбцы: {ordinate.Count}");
    }

    [ScriptFn("hist", "Гистограмма распределения", Returns = PlotHandle,
        Example = "show plot.hist(sample, bins: 20)")]
    public static ScriptHandle Histogram(
        [ScriptParam("выборка")] Vector sample,
        [ScriptParam("число интервалов")] int bins = 20,
        [ScriptParam("заголовок")] string title = "",
        [ScriptParam("подпись оси X")] string xlabel = "")
    {
        if (sample.Count == 0)
            throw new ScriptError(DiagnosticCodes.SizeMismatch, "plot.hist: выборка пуста");

        if (bins < 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "plot.hist: интервалов должно быть не меньше одного");

        double min = sample.Min();
        double width = sample.Max() - min;

        var centers = new double[bins];
        var counts = new double[bins];

        for (int i = 0; i < bins; i++) centers[i] = width == 0 ? min : min + (width * (i + 0.5) / bins);

        for (int i = 0; i < sample.Count; i++)
        {
            int bin = width == 0 ? 0 : (int)((sample[i] - min) / width * bins);
            counts[Math.Clamp(bin, 0, bins - 1)]++;
        }

        var builder = Builder(title, xlabel, "частота");
        builder.AddBar2D(centers, counts, null);

        return Figure(builder, title, $"гистограмма, интервалов: {bins}");
    }

    [ScriptFn("heatmap", "Тепловая карта матрицы", Returns = PlotHandle,
        Example = "show plot.heatmap(stat.confusion(y, pred), title: \"Матрица ошибок\")")]
    public static ScriptHandle Heatmap(
        [ScriptParam("матрица значений")] Matrix m,
        [ScriptParam("заголовок")] string title = "",
        [ScriptParam("подпись оси X")] string xlabel = "",
        [ScriptParam("подпись оси Y")] string ylabel = "",
        [ScriptParam("цветовая шкала")] string colors = "viridis")
    {
        if (m.Height == 0 || m.Width == 0)
            throw new ScriptError(DiagnosticCodes.SizeMismatch, "plot.heatmap: матрица пуста");

        var x = new double[m.Width];
        var y = new double[m.Height];
        var z = new double[m.Height][];

        for (int j = 0; j < m.Width; j++) x[j] = j;

        for (int i = 0; i < m.Height; i++)
        {
            y[i] = i;
            z[i] = new double[m.Width];

            for (int j = 0; j < m.Width; j++) z[i][j] = m[i, j];
        }

        var builder = Builder(title, xlabel, ylabel);
        builder.AddHeatmap(x, y, z, PlotlyBuilder.MapColorscale(colors), showScale: true);

        return Figure(builder, title, $"тепловая карта {m.Height}×{m.Width}");
    }

    [ScriptFn("spectrum", "График спектра по записи с полями freq и power либо amp", Returns = PlotHandle,
        Example = "show plot.spectrum(dsp.welch(signal, fs: 8000))")]
    public static ScriptHandle Spectrum(
        [ScriptParam("запись со спектром")] ScriptRecord spectrum,
        [ScriptParam("заголовок")] string title = "Спектр",
        [ScriptParam("логарифмическая шкала по Y")] bool log = false)
    {
        Vector frequency = Field(spectrum, "freq", "plot.spectrum");
        Vector power = spectrum.Has("power")
            ? Field(spectrum, "power", "plot.spectrum")
            : Field(spectrum, "amp", "plot.spectrum");

        var builder = Builder(title, "частота, Гц", "мощность");
        builder.IsLogY = log;
        builder.AddLine(frequency.ToArray(), power.ToArray(), null);

        return Figure(builder, title, $"спектр, точек: {power.Count}");
    }

    /// <summary>
    /// Несколько графиков одним артефактом.
    /// </summary>
    /// <remarks>
    /// Настоящая сетка подграфиков потребовала бы своего слоя над Plotly. Пока это набор
    /// самостоятельных графиков в одном артефакте — хост показывает их подряд. Скрипт при
    /// этом уже написан так, как надо, и менять его при появлении сетки не придётся.
    /// </remarks>
    [ScriptFn("grid", "Набор графиков одним артефактом", Returns = PlotHandle,
        Example = "show plot.grid([plot.line(x: t, y: a), plot.line(x: t, y: b)])")]
    public static ScriptHandle Grid(
        [ScriptParam("список графиков")] ScriptList figures,
        [ScriptParam("общий заголовок")] string title = "")
    {
        var parts = new List<PlotFigure>(figures.Count);

        for (int i = 0; i < figures.Count; i++)
        {
            if (figures[i].Type != ScriptType.Handle || figures[i].AsHandle().Target is not PlotFigure figure)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"plot.grid: элемент {i} не является графиком",
                    "в список передаются результаты plot.line, plot.scatter и подобных");
            }

            parts.Add(figure);
        }

        var combined = new PlotFigure(title, parts);

        return new ScriptHandle(PlotHandle, combined, $"набор из {parts.Count} графиков");
    }

    [ScriptFn("to_json", "Описание графика в формате Plotly", Example = "emit chart = plot.to_json(figure)")]
    [ScriptMethod(PlotHandle)]
    public static string ToJson([ScriptParam("график")] ScriptHandle figure) =>
        ((PlotFigure)figure.Target).ToJson();

    private static PlotlyBuilder Builder(string title, string xlabel, string ylabel) => new()
    {
        Title = string.IsNullOrWhiteSpace(title) ? null : title,
        AxisX = string.IsNullOrWhiteSpace(xlabel) ? null : xlabel,
        AxisY = string.IsNullOrWhiteSpace(ylabel) ? null : ylabel,
    };

    private static ScriptHandle Figure(PlotlyBuilder builder, string title, string summary) =>
        new(PlotHandle, new PlotFigure(title, builder), summary);

    private static string? Name(string name) => string.IsNullOrWhiteSpace(name) ? null : name;

    private static (Vector X, Vector Y) Pair(Vector series, Vector? abscissa, string what)
    {
        if (abscissa == null || abscissa.Count == 0)
        {
            var indices = new Vector(series.Count);

            for (int i = 0; i < series.Count; i++) indices[i] = i;

            return (indices, series);
        }

        RequireSameLength(abscissa, series, what);

        return (abscissa, series);
    }

    private static Vector Field(ScriptRecord record, string name, string what)
    {
        if (!record.TryGet(name, out ScriptValue value))
        {
            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{what}: в записи нет поля '{name}'",
                $"поля записи: {string.Join(", ", record.Keys)}");
        }

        return (Vector)Marshaller.ToClr(value, typeof(Vector), $"{what}: поле '{name}'")!;
    }

    private static void RequireSameLength(Vector x, Vector y, string what)
    {
        if (x.Count == y.Count) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"{what}: {x.Count} значений по X и {y.Count} по Y");
    }
}
