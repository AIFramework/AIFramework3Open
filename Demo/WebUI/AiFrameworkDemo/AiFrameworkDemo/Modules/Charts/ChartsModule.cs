using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Charts;

public sealed class ChartsModule : LibraryModuleBase
{
    public override string Id => "charts";
    public override string Name => "AI.Charts";
    public override string Description => "Кроссплатформенные графики через SkiaSharp: Plot, Bar, Scatter, Polar, Pie, DSP-спектр";
    public override string Color => "sky";
    public override string TutorialFolder => "Charts";
    public override string IconSvg => """<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><path d="M18 20V10M12 20V4M6 20v-6"/><line x1="2" y1="21" x2="22" y2="21"/></svg>""";

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new("line", "Линейные и сплайновые", "Plot + Spline + ComplexVector",
            [
                new("line_sin_cos", "Sin + Cos", "Plot: две серии", "ChartView.AddPlot", "ChartViewAndControls.md", []),
                new("line_spline", "Сплайн vs линия", "isSpline=true/false", "ChartView.AddPlot", "ChartViewAndControls.md", []),
                new("line_decay", "Затухающий сигнал", "e^(-t)·sin(t)", "ChartView.AddPlot", "ChartViewAndControls.md", []),
                new("line_complex", "Комплексный сигнал", "PlotComplex: Re и Im", "ChartView.PlotComplex", "ChartViewAndControls.md", []),
            ]),
        new("bar", "Столбчатые и площадные", "Bar + Area + Histogram",
            [
                new("bar_basic", "Столбчатая", "AddBar", "ChartView.AddBar", "ChartViewAndControls.md", []),
                new("bar_area", "Area (Площадь)", "AddArea", "ChartView.AddArea", "ChartViewAndControls.md", []),
                new("bar_histogram", "Гистограмма", "AddHistoramm", "ChartView.AddHistoramm", "ChartViewAndControls.md", []),
            ]),
        new("scatter", "Точечные диаграммы", "Scatter + ComплексPlane",
            [
                new("sc_clusters", "Два кластера", "AddScatter × 2", "ChartView.AddScatter", "ChartViewAndControls.md", []),
                new("sc_spiral", "Спираль Архимеда", "AddScatter (spiral)", "ChartView.AddScatter", "ChartViewAndControls.md", []),
                new("sc_complex", "Комплексная плоскость", "ScatterComplexPlane", "ChartView.ScatterComplexPlane", "ChartViewAndControls.md", []),
            ]),
        new("polar", "Полярные графики", "Radial + Кардиоида + Роза",
            [
                new("pol_rose4", "Роза (4 лепестка)", "r=|cos(4θ)|", "ChartView.AddRadialDegPlot", "ChartViewAndControls.md", []),
                new("pol_cardioid", "Кардиоида", "r=1+cos(θ)", "ChartView.AddRadialDegPlot", "ChartViewAndControls.md", []),
                new("pol_vector", "Вектор полярно", "RadPlotBlueDeg(Vector)", "ChartView.RadPlotBlueDeg", "ChartViewAndControls.md", []),
            ]),
        new("pie", "Круговые диаграммы", "Секторная диаграмма",
            [
                new("pie_basic", "Круговая диаграмма", "AddCircul", "ChartView.AddCircul", "ChartViewAndControls.md", []),
            ]),
        new("signal", "Сигналы и DSP", "FFT-спектр, производная, интеграл",
            [
                new("sig_spectrum", "FFT-спектр", "AddSpectrum (Хэмминг)", "ChartView.AddSpectrum", "ChartViewAndControls.md", []),
                new("sig_diff", "Производная", "AddDiff dy/dx", "ChartView.AddDiff", "ChartViewAndControls.md", []),
                new("sig_integ", "Интеграл", "AddIntegr ∫y dx", "ChartView.AddIntegr", "ChartViewAndControls.md", []),
            ]),
        new("multi", "Многосерийные и логшкала", "Несколько серий + LogScale",
            [
                new("multi_4sin", "4 синусоиды", "AddPlot × 4 (автопалитра)", "ChartView.AddPlot", "ChartViewAndControls.md", []),
                new("multi_log", "Логарифмическая ось", "IsLogScale = true", "ChartView.IsLogScale", "ChartViewAndControls.md", []),
            ]),
        new("3d", "3D-графики", "Surface, Wireframe, Scatter3D",
            [
                new("3d_surface", "Surface (поверхность)", "Залитая поверхность Z(x,y)", "ChartView.AddSurface", "Charts3D.md",
                    [
                        new("azimuth",   "Азимут камеры",   0, 360, 45, 5, "°"),
                        new("elevation", "Наклон камеры", -89,  89, 30, 1, "°"),
                    ]),
                new("3d_wireframe", "Wireframe (каркас)", "Каркасная сетка Z(x,y)", "ChartView.AddWireframe", "Charts3D.md",
                    [
                        new("azimuth",   "Азимут камеры",   0, 360, 45, 5, "°"),
                        new("elevation", "Наклон камеры", -89,  89, 30, 1, "°"),
                    ]),
                new("3d_scatter", "Scatter 3D (облако)", "Точечное облако в 3D", "ChartView.AddScatter3D", "Charts3D.md",
                    [
                        new("azimuth",   "Азимут камеры",   0, 360, 45, 5, "°"),
                        new("elevation", "Наклон камеры", -89,  89, 30, 1, "°"),
                    ]),
                new("3d_peaks", "Peaks (MATLAB-style)", "Классическая поверхность peaks", "ChartView.AddSurface", "Charts3D.md",
                    [
                        new("azimuth",   "Азимут камеры",   0, 360, 55, 5, "°"),
                        new("elevation", "Наклон камеры", -89,  89, 25, 1, "°"),
                    ]),
            ]),
    ];

    protected override DemoResult RunCore(string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string>  textParams,
        DemoSettings settings)
    {
        var (png, plotlyJson, cv) = ChartDemoRunner.Render(
            algoKey, numericParams, settings.Width, settings.Height, settings.DarkTheme);
        return new DemoResult { PngDataUrl = png, PlotlyJson = plotlyJson, SourceChart = cv };
    }
}
