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
    public static DemoResult Png(ChartView cv, DemoSettings s,
        string? plotlyJson = null, string? textOutput = null) =>
        new()
        {
            PngDataUrl  = RenderPng(cv, s),
            PlotlyJson  = plotlyJson ?? PlotlyChartRenderer.ToPlotlyJson(cv),
            SourceChart = cv,
            TextOutput  = textOutput,
        };

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
