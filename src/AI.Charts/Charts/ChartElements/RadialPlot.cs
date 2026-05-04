using AI.Charts.Data;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using System;
using SkiaSharp;

namespace AI.Charts.ChartElements;

[Serializable]
internal class RadialPlot : BaseChart
{
    public RadialPlot(string name) : base(name)
    {
        LayoutKind = ChartLayoutKind.Polar;
    }

    public override Tuple<Vector, Vector> ReducMethod(Vector xN, Vector yN)
    {
        return DataMethods.ReducDataRadialPlot(xN, yN);
    }

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        if (drawX == null || drawY == null || drawX.Count < 2)
        {
            return;
        }

        using (SKPath path = new SKPath())
        {
            SKPoint p0 = vp.PolarToPx(drawX[0], drawY[0]);
            path.MoveTo(p0);
            int n = drawX.Count;
            for (int i = 1; i < n; i++)
            {
                SKPoint pi = vp.PolarToPx(drawX[i], drawY[i]);
                path.LineTo(pi);
            }

            using (SKPaint paint = new SKPaint
            {
                Color = ElementColor,
                StrokeWidth = BorderWidth,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            })
            {
                canvas.DrawPath(path, paint);
            }
        }
    }
}
