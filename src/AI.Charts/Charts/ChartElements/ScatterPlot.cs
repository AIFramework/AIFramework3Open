using AI.Charts.Data;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using System;
using SkiaSharp;

namespace AI.Charts.ChartElements;

[Serializable]
internal class ScatterPlot : BaseChart
{
    private int markerSize = 6;

    public ScatterPlot(string name) : base(name)
    {
    }

    public void SetMarkSize(int markSize)
    {
        markerSize = markSize;
    }

    public void AutoSetMarkSize()
    {
        int markSize = 5;
        if (Data != null && Data.Count < 10000)
        {
            markSize = 7;
            if (Data.Count < 5000)
            {
                markSize = 14;
            }
        }

        SetMarkSize(markSize);
    }

    public override void Recalc(double min, double max)
    {
        if (data == null)
        {
            return;
        }

        Vector xN = data.GetX();
        Vector yN = data.GetY();
        Tuple<Vector, Vector> dat = ReducMethod(xN, yN);
        xN = dat.Item1;
        yN = dat.Item2;
        drawX = xN;
        drawY = yN;
    }

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        if (drawX == null || drawY == null || drawX.Count == 0)
        {
            return;
        }

        float r = markerSize * 0.5f;
        using (SKPaint paint = new SKPaint
        {
            Color = ElementColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        })
        {
            int n = drawX.Count;
            for (int i = 0; i < n; i++)
            {
                float px = vp.XToPx(drawX[i]);
                float py = vp.YToPx(drawY[i]);
                canvas.DrawCircle(px, py, r, paint);
            }
        }
    }
}
