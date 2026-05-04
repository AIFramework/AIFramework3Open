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

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        if (drawX == null || drawY == null || drawX.Count == 0)
        {
            return;
        }

        SKColor fill = ElementColor.WithAlpha(90);

        using (SKPath path = new SKPath())
        {
            float x0 = vp.XToPx(drawX[0]);
            float y0 = vp.YToPx(drawY[0]);
            path.MoveTo(x0, vp.PlotRect.Bottom);
            path.LineTo(x0, y0);
            int n = drawX.Count;
            for (int i = 1; i < n; i++)
            {
                path.LineTo(vp.XToPx(drawX[i]), vp.YToPx(drawY[i]));
            }

            path.LineTo(vp.XToPx(drawX[n - 1]), vp.PlotRect.Bottom);
            path.Close();

            using (SKPaint fillPaint = new SKPaint { Color = fill.WithAlpha(100), Style = SKPaintStyle.Fill, IsAntialias = true })
            using (SKPaint strokePaint = new SKPaint
            {
                Color = ElementColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderWidth,
                IsAntialias = true
            })
            {
                canvas.DrawPath(path, fillPaint);
                path.Reset();
                path.MoveTo(x0, y0);
                for (int i = 1; i < n; i++)
                {
                    path.LineTo(vp.XToPx(drawX[i]), vp.YToPx(drawY[i]));
                }

                canvas.DrawPath(path, strokePaint);
            }
        }
    }
}
