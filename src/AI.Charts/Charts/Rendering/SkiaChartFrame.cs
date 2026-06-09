using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace AI.Charts.Rendering;

/// <summary>
/// Оси, сетка, заголовок и легенда (декартова плоскость).
/// </summary>
internal static partial class SkiaChartFrame
{
    private const int DefaultGridY = 5;
    private const float MinPlotInner = 48f;

    /// <summary>
    /// Подбирает <see cref="ChartViewport.PlotRect"/> и деления сетки так, чтобы подписи не заходили в область графика.
    /// Перегрузка с тремя параметрами сохранена для бинарной совместимости (старые сборки не ищут подпись оси Y).
    /// </summary>
    public static void LayoutCartesianMargins(ChartViewport vp, float w, float h)
    {
        LayoutCartesianMargins(vp, w, h, null, null);
    }

    /// <summary>
    /// Подбирает <see cref="ChartViewport.PlotRect"/>; <paramref name="axisYLabel"/> учитывается в левом поле под вертикальную подпись.
    /// </summary>
    public static void LayoutCartesianMargins(ChartViewport vp, float w, float h, string axisYLabel)
    {
        LayoutCartesianMargins(vp, w, h, axisYLabel, null);
    }

    /// <summary>
    /// Подбирает поля; <paramref name="axisXLabel"/> увеличивает нижнее поле под подписи X и числа, не наезжающие на текст оси.
    /// </summary>
    public static void LayoutCartesianMargins(ChartViewport vp, float w, float h, string axisYLabel, string axisXLabel)
    {
        const float mt = 24f;
        const float mbMin = 38f;

        vp.GridDivisionsY = DefaultGridY;

        using (SKPaint measurePaint = new SKPaint
        {
            TextSize = 11,
            Typeface = SKTypeface.FromFamilyName("Segoe UI"),
            IsAntialias = true
        })
        {
            float maxYLabel = 0f;
            for (int i = 0; i <= vp.GridDivisionsY; i++)
            {
                double yv = GetYTickValue(vp, i, vp.GridDivisionsY);
                maxYLabel = Math.Max(maxYLabel, measurePaint.MeasureText(FormatTick(yv)));
            }

            float yTitleBand = string.IsNullOrEmpty(axisYLabel)
                ? 0f
                : measurePaint.MeasureText(axisYLabel) + 18f;
            float ml = Math.Max(50f, maxYLabel + 16f + yTitleBand);

            const float mrProbe = 16f;
            float innerW = w - ml - mrProbe;
            if (innerW < MinPlotInner)
            {
                innerW = MinPlotInner;
            }

            int xDiv = ChooseXDivisions(vp, innerW, measurePaint);

            float maxXLabel = 0f;
            for (int i = 0; i <= xDiv; i++)
            {
                double xv = GetXTickValue(vp, i, xDiv);
                maxXLabel = Math.Max(maxXLabel, measurePaint.MeasureText(FormatTick(xv)));
            }

            float mr = Math.Max(22f, maxXLabel * 0.52f + 14f);

            float innerPlotW = Math.Max(MinPlotInner, w - ml - mr);
            float slotW = innerPlotW / Math.Max(xDiv, 1);
            bool xCrowded = slotW < maxXLabel + 12f;
            float mb = xCrowded ? Math.Max(mbMin, 52f) : mbMin;
            mb = Math.Max(mb, 18f + (xCrowded ? 28f : 14f));
            if (!string.IsNullOrEmpty(axisXLabel))
            {
                mb += 22f;
            }

            vp.GridDivisionsX = xDiv;
            float bottom = h - mb;
            if (bottom <= mt + MinPlotInner)
            {
                bottom = mt + MinPlotInner;
            }

            vp.PlotRect = new SKRect(ml, mt, w - mr, bottom);
        }
    }

    private static int ChooseXDivisions(ChartViewport vp, float innerW, SKPaint paint)
    {
        for (int xDiv = 8; xDiv >= 3; xDiv--)
        {
            float maxLen = 0f;
            for (int i = 0; i <= xDiv; i++)
            {
                double xv = GetXTickValue(vp, i, xDiv);
                maxLen = Math.Max(maxLen, paint.MeasureText(FormatTick(xv)));
            }

            float slot = innerW / Math.Max(xDiv, 1);
            if (slot >= maxLen + 10f || xDiv == 3)
            {
                return xDiv;
            }
        }

        return 3;
    }

    private static double GetXTickValue(ChartViewport vp, int i, int div)
    {
        return vp.XMin + (vp.XMax - vp.XMin) * i / div;
    }

    private static double GetYTickValue(ChartViewport vp, int i, int div)
    {
        if (vp.LogY)
        {
            double l0 = Math.Log10(Math.Max(vp.YMin, 1e-300));
            double l1 = Math.Log10(Math.Max(vp.YMax, 1e-300));
            return Math.Pow(10, l0 + (l1 - l0) * (div - i) / div);
        }

        return vp.YMin + (vp.YMax - vp.YMin) * (div - i) / div;
    }

    public static void DrawPolarFrame(SKCanvas canvas, ChartViewport vp, string title, string axisYLabel)
    {
        canvas.Clear(vp.Background);
        using (SKPaint gridPaint = new SKPaint { Color = vp.Grid, StrokeWidth = 1, IsAntialias = true })
        using (SKPaint axisPaint = new SKPaint { Color = vp.Foreground, StrokeWidth = 1.5f, IsAntialias = true })
        using (SKPaint textPaint = new SKPaint
        {
            Color = vp.Foreground,
            IsAntialias = true,
            TextSize = 11,
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        })
        {
            float cx = vp.PolarCx;
            float cy = vp.PolarCy;
            float r = vp.PolarRingRadiusPx;
            for (int i = 1; i <= 4; i++)
            {
                float ri = r * i / 4f;
                canvas.DrawCircle(cx, cy, ri, gridPaint);
            }

            for (int i = 0; i < 8; i++)
            {
                double ang = i * Math.PI / 4;
                float x2 = cx + r * (float)Math.Cos(ang);
                float y2 = cy - r * (float)Math.Sin(ang);
                canvas.DrawLine(cx, cy, x2, y2, gridPaint);
            }

            canvas.DrawCircle(cx, cy, r, axisPaint);
            if (!string.IsNullOrEmpty(title))
            {
                canvas.DrawText(title, cx - title.Length * 3.2f, cy - r - 12, textPaint);
            }

            if (!string.IsNullOrEmpty(axisYLabel))
            {
                canvas.DrawText(axisYLabel, cx - 40, cy + r + 22, textPaint);
            }
        }
    }

    public static void DrawPieFrame(SKCanvas canvas, ChartViewport vp, string title)
    {
        canvas.Clear(vp.Background);
        using (SKPaint textPaint = new SKPaint
        {
            Color = vp.Foreground,
            IsAntialias = true,
            TextSize = 12,
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        })
        {
            if (!string.IsNullOrEmpty(title))
            {
                canvas.DrawText(title, vp.PieCx - title.Length * 3.2f, vp.PieCy - vp.PieRadius - 16, textPaint);
            }
        }
    }

    private static string FormatTick(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
        {
            return "";
        }

        double a = Math.Abs(v);
        if (a >= 10000 || (a > 0 && a < 1e-4))
        {
            return v.ToString("G4", CultureInfo.InvariantCulture);
        }

        if (a >= 0.01 && a < 1000)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return Math.Round(v, 4).ToString(CultureInfo.InvariantCulture);
    }
}
