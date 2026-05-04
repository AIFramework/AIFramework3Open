using System;
using AI.Charts.Rendering;
using SkiaSharp;

namespace AI.Charts.ChartElements;

[Serializable]
internal class Plot : BaseChart
{
    private bool isSpline;

    public bool IsSpline
    {
        get => isSpline;
        set => isSpline = value;
    }

    public Plot(string name) : base(name)
    {
    }

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        if (drawX == null || drawY == null || drawX.Count == 0)
        {
            return;
        }

        using (SKPath path = new SKPath())
        {
            SplinePath.AppendLineOrSpline(path, drawX, drawY, vp, isSpline);
            using (SKPaint paint = new SKPaint
            {
                Color = ElementColor,
                StrokeWidth = BorderWidth,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Round
            })
            {
                canvas.DrawPath(path, paint);
            }
        }
    }
}
