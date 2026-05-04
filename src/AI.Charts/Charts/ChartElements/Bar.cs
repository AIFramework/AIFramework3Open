using System;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.Charts.ChartElements;

[Serializable]
internal class Bar : BaseChart
{
    public Bar(string name) : base(name)
    {
    }

    /// <summary>
    /// Столбцы не прореживаем: иначе при масштабировании ломается шаг по X и появляется «штрих-код».
    /// </summary>
    public override Tuple<Vector, Vector> ReducMethod(Vector xN, Vector yN)
    {
        return new Tuple<Vector, Vector>(xN, yN);
    }

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        if (drawX == null || drawY == null || drawX.Count == 0)
        {
            return;
        }

        SKRect plot = vp.PlotRect;
        int n = drawX.Count;

        using (SKPaint paint = new SKPaint
        {
            Color = ElementColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        })
        {
            canvas.Save();
            canvas.ClipRect(plot);

            for (int i = 0; i < n; i++)
            {
                float cx = vp.XToPx(drawX[i]);
                float stepPx;
                if (n == 1)
                {
                    stepPx = plot.Width * 0.8f;
                }
                else
                {
                    float distPrev = i > 0 ? Math.Abs(cx - vp.XToPx(drawX[i - 1])) : float.MaxValue;
                    float distNext = i < n - 1 ? Math.Abs(vp.XToPx(drawX[i + 1]) - cx) : float.MaxValue;
                    stepPx = Math.Min(distPrev, distNext);
                    if (stepPx > plot.Width || float.IsInfinity(stepPx))
                    {
                        stepPx = plot.Width / Math.Max(n, 1);
                    }
                }

                float barW = Math.Max(2f, stepPx * 0.65f);
                float x0 = cx - barW * 0.5f;
                float y0 = vp.YToPx(drawY[i]);
                float y1 = plot.Bottom;
                if (y0 > y1)
                {
                    float t = y0;
                    y0 = y1;
                    y1 = t;
                }

                y0 = Math.Max(plot.Top, Math.Min(y0, plot.Bottom));
                y1 = Math.Max(plot.Top, Math.Min(y1, plot.Bottom));
                if (y1 <= y0)
                {
                    continue;
                }

                canvas.DrawRect(x0, y0, barW, y1 - y0, paint);
            }

            canvas.Restore();
        }
    }
}
