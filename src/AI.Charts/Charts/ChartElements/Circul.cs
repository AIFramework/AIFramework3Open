using System;
using AI.Charts.Rendering;
using SkiaSharp;

namespace AI.Charts.ChartElements;

[Serializable]
internal class Circul : BaseChart
{
    private static readonly SKColor[] SlicePalette =
    {
        new SKColor(220, 80, 80),
        new SKColor(80, 180, 120),
        new SKColor(80, 120, 220),
        new SKColor(220, 180, 60),
        new SKColor(180, 80, 200),
        new SKColor(100, 200, 200),
        new SKColor(240, 140, 80)
    };

    public Circul(string name) : base(name)
    {
        LayoutKind = ChartLayoutKind.Pie;
    }

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        if (drawX == null || drawY == null || drawY.Count == 0)
        {
            return;
        }

        double sum = 0;
        int n = drawY.Count;
        for (int i = 0; i < n; i++)
        {
            double v = drawY[i];
            if (v > 0)
            {
                sum += v;
            }
        }

        if (sum <= 1e-30)
        {
            return;
        }

        float cx = vp.PieCx;
        float cy = vp.PieCy;
        float r = vp.PieRadius;
        double start = -Math.PI / 2;
        SKColor baseC = ElementColor;
        for (int i = 0; i < n; i++)
        {
            double v = drawY[i];
            if (v <= 0)
            {
                continue;
            }

            double sweep = 2 * Math.PI * (v / sum);
            SKColor sliceColor = n > 1 ? SlicePalette[i % SlicePalette.Length] : baseC;

            using (SKPath path = new SKPath())
            {
                path.MoveTo(cx, cy);
                const int segments = 48;
                for (int s = 0; s <= segments; s++)
                {
                    double a = start + sweep * s / segments;
                    path.LineTo(cx + r * (float)Math.Cos(a), cy - r * (float)Math.Sin(a));
                }

                path.Close();

                using (SKPaint paint = new SKPaint { Color = sliceColor.WithAlpha(230), Style = SKPaintStyle.Fill, IsAntialias = true })
                {
                    canvas.DrawPath(path, paint);
                }

                using (SKPaint outline = new SKPaint { Color = vp.Foreground, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true })
                {
                    canvas.DrawPath(path, outline);
                }
            }

            start += sweep;
        }
    }
}
