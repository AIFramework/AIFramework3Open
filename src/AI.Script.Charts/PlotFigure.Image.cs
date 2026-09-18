using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Script.Runtime;
using AI.Script.Semantics;
using SkiaSharp;

namespace AI.Script.Charts;

/// <summary>
/// График картинкой: для отчёта в Word и для всего, что не исполняет Plotly.
/// </summary>
/// <remarks>
/// Рисует тот же движок графиков фреймворка, что и настольные окна, — отдельного рисовальщика
/// для отчётов нет, и вид графика в приложении и в документе не разъезжается.
/// <para>
/// Тепловую карту так пока не нарисовать: у движка нет такого вида. Отказ говорит об этом прямо,
/// а не вставляет в отчёт пустую рамку.
/// </para>
/// </remarks>
public sealed partial class PlotFigure
{
    /// <inheritdoc/>
    public string ImageTitle => Title;

    /// <inheritdoc/>
    public byte[] RenderPng(int width, int height)
    {
        if (IsGrid) return Stack(width, height);

        if (_series.Count == 0)
        {
            throw new ScriptError(
                DiagnosticCodes.NotImplementedYet,
                $"график «{Title}» картинкой не рисуется: такой вид есть только в браузере",
                "в документ встраиваются линии, точки, столбцы и гистограммы");
        }

        using SKBitmap bitmap = View().ToBitmap(width, height);

        return Encode(bitmap);
    }

    /// <summary>Цвета серий по порядку: у картинки легенда читается только по цвету.</summary>
    private static readonly SKColor[] s_palette =
    [
        new(0, 120, 215), new(220, 53, 69), new(40, 167, 69), new(255, 152, 0),
        new(123, 31, 162), new(0, 150, 136), new(121, 85, 72), new(63, 81, 181),
    ];

    private ChartView View()
    {
        // Шкала та же, что у описания для браузера: иначе картинка в отчете и график в ленте
        // показывали бы разное.
        var view = new ChartView
        {
            ChartName = Title,
            LabelX = _xlabel,
            LabelY = _ylabel,
            IsLogScale = _builder?.IsLogY == true,
        };

        for (int i = 0; i < _series.Count; i++)
        {
            PlotSeries series = _series[i];
            var x = new Vector(series.X);
            var y = new Vector(series.Y);
            string name = series.Name ?? string.Empty;
            SKColor color = s_palette[i % s_palette.Length];

            switch (series.Kind)
            {
                case "scatter":
                    view.AddScatter(x, y, name, color);
                    break;

                case "bar":
                    view.AddBar(x, y, name, color);
                    break;

                default:
                    view.AddPlot(x, y, name, color);
                    break;
            }
        }

        return view;
    }

    /// <summary>Набор графиков — столбиком, каждый своей полосой одной высоты.</summary>
    private byte[] Stack(int width, int height)
    {
        int band = Math.Max(1, height / Math.Max(1, _parts.Count));

        using var bitmap = new SKBitmap(width, band * _parts.Count);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.White);

        for (int i = 0; i < _parts.Count; i++)
        {
            using SKBitmap part = SKBitmap.Decode(_parts[i].RenderPng(width, band));

            canvas.DrawBitmap(part, 0, i * band);
        }

        return Encode(bitmap);
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
}
