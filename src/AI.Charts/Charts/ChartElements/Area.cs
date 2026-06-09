using System;
using AI.Charts.Rendering;
using SkiaSharp;

namespace AI.Charts.ChartElements;

[Serializable]
internal class Area : BaseChart
{
    public Area(string name) : base(name)
    {
    }

    /// <summary>Сглаживать ли верхнюю границу области сплайном (Catmull–Rom).</summary>
    public bool IsSpline { get; set; }

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        if (drawX == null || drawY == null || drawX.Count == 0)
        {
            return;
        }

        int n = drawX.Count;
        float bottom = vp.PlotRect.Bottom;
        float xFirst = vp.XToPx(drawX[0]);
        float xLast = vp.XToPx(drawX[n - 1]);

        // Верхняя граница (линия или сплайн) — переиспользуем общий построитель пути.
        using (SKPath topPath = new SKPath())
        {
            SplinePath.AppendLineOrSpline(topPath, drawX, drawY, vp, IsSpline);

            // Заливка: замыкаем контур к низу области и заливаем вертикальным градиентом
            // от насыщенного цвета сверху к прозрачному снизу — современный «area»-вид.
            using (SKPath fillPath = new SKPath(topPath))
            {
                fillPath.LineTo(xLast, bottom);
                fillPath.LineTo(xFirst, bottom);
                fillPath.Close();

                using (SKShader shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, vp.PlotRect.Top),
                    new SKPoint(0, bottom),
                    new[] { ElementColor.WithAlpha(130), ElementColor.WithAlpha(8) },
                    new[] { 0f, 1f },
                    SKShaderTileMode.Clamp))
                using (SKPaint fillPaint = new SKPaint { Shader = shader, Style = SKPaintStyle.Fill, IsAntialias = true })
                {
                    canvas.DrawPath(fillPath, fillPaint);
                }
            }

            using (SKPaint strokePaint = new SKPaint
            {
                Color = ElementColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderWidth,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Round
            })
            {
                canvas.DrawPath(topPath, strokePaint);
            }
        }
    }
}
