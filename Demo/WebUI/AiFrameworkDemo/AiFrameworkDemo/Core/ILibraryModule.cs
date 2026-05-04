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

public record DemoResult
{
    public string? PngDataUrl { get; init; }
    public string? TextOutput { get; init; }
    public string? Error { get; init; }
    public bool NeedsImageUpload { get; init; }

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
