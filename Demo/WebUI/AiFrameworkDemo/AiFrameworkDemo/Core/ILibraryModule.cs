namespace AiFrameworkDemo.Core;

public interface ILibraryModule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string IconSvg { get; }
    string Color { get; }
    string TutorialFolder { get; }
    IReadOnlyList<CategoryDef> Categories { get; }
    DemoResult RunDemo(string algoKey, IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams, DemoSettings settings);
}

public record CategoryDef(string Id, string Title, string? Summary, IReadOnlyList<AlgoDef> Algorithms);

public record AlgoDef(
    string Key,
    string Title,
    string Subtitle,
    string ApiClass,
    string TheoryFile,
    IReadOnlyList<AlgoParam> Params);

public record AlgoParam(
    string Key,
    string Label,
    double Min,
    double Max,
    double Default,
    double Step,
    string Unit = "",
    string Hint = "",
    string TextDefault = "")
{
    /// <summary>
    /// Если задан — параметр рендерится как сегментированный выбор (pill-buttons),
    /// а не как ползунок. Поле <see cref="Min"/>/<see cref="Max"/>/<see cref="Step"/> игнорируется.
    /// </summary>
    public IReadOnlyList<AlgoChoice>? Choices { get; init; }
}

public record AlgoChoice(double Value, string Label, string? Icon = null);

public record DemoSettings
{
    public int Width { get; init; } = 680;
    public int Height { get; init; } = 360;
    public bool DarkTheme { get; init; } = true;
}

/// <summary>Тон метрики: подсветка ключевого числа без чтения всего лога.</summary>
public enum MetricTone { Neutral, Good, Warn, Bad }

/// <summary>
/// Ключевое число результата — выносится плашкой над графиком.
/// Не более 4–5 штук на демо: смысл в том, чтобы главное было видно сразу.
/// </summary>
/// <param name="Label">Что измерено, например «Лучший документ».</param>
/// <param name="Value">Значение уже отформатированной строкой.</param>
/// <param name="Unit">Единица измерения, если есть.</param>
/// <param name="Hint">Пояснение во всплывающей подсказке.</param>
public record DemoMetric(
    string Label,
    string Value,
    string? Unit = null,
    string? Hint = null,
    MetricTone Tone = MetricTone.Neutral);

/// <summary>
/// Таблица результата вместо выровненных пробелами строк в моноширинном логе.
/// </summary>
/// <param name="Numeric">
/// Флаги «колонка числовая» по индексу: такие выравниваются вправо
/// и получают табличные цифры. Может быть короче списка заголовков.
/// </param>
public record DemoTable(
    string Title,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<bool>? Numeric = null,
    string? Note = null);

/// <summary>
/// Структурированный результат демо: метрики + таблицы.
/// Дополняет, а не заменяет <see cref="DemoResult.TextOutput"/> — полный
/// текстовый лог остаётся доступным под спойлером.
/// </summary>
public record DemoReport
{
    public IReadOnlyList<DemoMetric> Metrics { get; init; } = [];
    public IReadOnlyList<DemoTable>  Tables  { get; init; } = [];

    /// <summary>Пояснение под метриками: как читать результат.</summary>
    public string? Note { get; init; }

    public bool IsEmpty => Metrics.Count == 0 && Tables.Count == 0 && string.IsNullOrEmpty(Note);
}

public record DemoResult
{
    public string? PngDataUrl { get; init; }
    public string? TextOutput { get; init; }
    public string? Error { get; init; }
    public bool NeedsImageUpload { get; init; }

    /// <summary>
    /// Структурированный вывод: метрики и таблицы. Когда задан, UI показывает
    /// его вместо «стены» моноширинного текста, а сам текст прячет под спойлер.
    /// </summary>
    public DemoReport? Report { get; init; }

    /// <summary>
    /// JSON for interactive Plotly.js 3D chart. When set, the UI renders
    /// an interactive chart instead of the static PNG.
    /// </summary>
    public string? PlotlyJson { get; init; }

    /// <summary>
    /// Original ChartView used to build PlotlyJson.
    /// Kept alive so context-menu transforms (FFT, derivative, integral, histogram)
    /// can be computed server-side using the framework's math libraries.
    /// </summary>
    public AI.Charts.ChartView? SourceChart { get; init; }
}
