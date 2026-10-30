using AI.Charts;
using AI.Charts.JS;
using AI.DataStructs.Algebraic;
using AiFrameworkDemo.Core;
using SkiaSharp;

namespace AiFrameworkDemo.Core;

/// <summary>
/// Вспомогательные утилиты для всех DemoRunner-ов.
/// Устраняет дублирование MakeView / RenderPng / ToArray / N() между модулями.
///
/// Использование:
/// <code>
///   using static AiFrameworkDemo.Core.DemoRunnerBase;
///
///   var cv  = MakeView(settings);
///   double k = N(p, "k", 3);
///   return Png(cv, settings);
/// </code>
/// </summary>
public static class DemoRunnerBase
{
    // -- ChartView factory -----------------------------------------------------

    /// <summary>Создаёт ChartView с тёмной или светлой темой из настроек.</summary>
    public static ChartView MakeView(DemoSettings s) =>
        MakeView(s.Width, s.Height, s.DarkTheme);

    /// <summary>Создаёт ChartView с явно заданными параметрами.</summary>
    public static ChartView MakeView(int width, int height, bool darkTheme)
    {
        var cv = new ChartView();
        if (darkTheme)
        {
            cv.BackgroundColor = new SKColor(0x0F, 0x13, 0x23);
            cv.ForegroundColor = new SKColor(0xC9, 0xD0, 0xE0);
        }
        else
        {
            cv.BackgroundColor = SKColors.White;
            cv.ForegroundColor = SKColors.Black;
        }
        return cv;
    }

    // -- Рендеринг -------------------------------------------------------------

    /// <summary>Рендерит ChartView в PNG data URL.</summary>
    public static string RenderPng(ChartView cv, DemoSettings s) =>
        RenderPng(cv, s.Width, s.Height);

    /// <summary>Рендерит ChartView в PNG data URL (явные размеры).</summary>
    public static string RenderPng(ChartView cv, int width, int height)
    {
        using var bmp     = cv.ToBitmap(width, height);
        using var img     = SKImage.FromBitmap(bmp);
        using var encoded = img.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(encoded.ToArray());
    }

    /// <summary>
    /// Создаёт DemoResult с PNG и опциональным Plotly JSON за один вызов.
    /// </summary>
    /// <param name="report">
    /// Структурированный вывод (метрики и таблицы). Когда задан, UI показывает
    /// его, а текстовый лог убирает под спойлер. См. <see cref="ReportBuilder"/>.
    /// </param>
    public static DemoResult Png(ChartView cv, DemoSettings s,
        string? plotlyJson = null, string? textOutput = null, DemoReport? report = null) =>
        new()
        {
            PngDataUrl  = RenderPng(cv, s),
            PlotlyJson  = plotlyJson ?? PlotlyChartRenderer.ToPlotlyJson(cv),
            SourceChart = cv,
            TextOutput  = textOutput,
            Report      = report,
        };

    /// <summary>Цвет шкалы Viridis для значения от 0 до 1 — для тепловых карт на фоне графика.</summary>
    public static SKColor ViridisColor(double t, byte alpha = 255)
    {
        (double t, byte r, byte g, byte b)[] stops = [(0.00, 0x44, 0x01, 0x54), (0.25, 0x3B, 0x52, 0x8B), (0.50, 0x20, 0x90, 0x8C), (0.75, 0x5E, 0xC9, 0x62), (1.00, 0xFD, 0xE7, 0x25)];
        for (int i = 0; i < stops.Length - 1; i++)
        {
            if (t <= stops[i + 1].t) { double k = (t - stops[i].t) / (stops[i + 1].t - stops[i].t); return new SKColor((byte)(stops[i].r + k * (stops[i + 1].r - stops[i].r)), (byte)(stops[i].g + k * (stops[i + 1].g - stops[i].g)), (byte)(stops[i].b + k * (stops[i + 1].b - stops[i].b)), alpha); }
        }
        return new SKColor(stops[^1].r, stops[^1].g, stops[^1].b, alpha);
    }

    // -- Параметры -------------------------------------------------------------

    /// <summary>Безопасно читает числовой параметр из словаря.</summary>
    public static double N(IReadOnlyDictionary<string, double> p, string key, double def = 0) =>
        p != null && p.TryGetValue(key, out var v) ? v : def;

    /// <summary>Читает числовой параметр и приводит к int.</summary>
    public static int I(IReadOnlyDictionary<string, double> p, string key, int def = 0) =>
        (int)N(p, key, def);

    /// <summary>Безопасно читает текстовый параметр из словаря.</summary>
    public static string T(IReadOnlyDictionary<string, string>? tp, string key, string def = "") =>
        tp != null && tp.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;

    // -- Конвертация -----------------------------------------------------------

    /// <summary>Конвертирует Vector в double[].</summary>
    public static double[] ToArray(Vector v)
    {
        var a = new double[v.Count];
        for (int i = 0; i < v.Count; i++) a[i] = v[i];
        return a;
    }
}
