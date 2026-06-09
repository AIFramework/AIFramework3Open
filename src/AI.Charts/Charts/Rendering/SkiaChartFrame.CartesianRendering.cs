using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace AI.Charts.Rendering;

internal static partial class SkiaChartFrame
{
    /// <summary>
    /// Рамка декартова графика (сетка, оси). Легенду рисуйте отдельно через <see cref="DrawCartesianLegend"/> после серий.
    /// </summary>
    public static void DrawCartesianFrame(
        SKCanvas canvas,
        ChartViewport vp,
        string title,
        string axisX,
        string axisY,
        SKImage backgroundImage)
    {
        canvas.Clear(vp.Background);

        if (backgroundImage != null)
        {
            using (SKPaint p = new SKPaint { FilterQuality = SKFilterQuality.Low })
            {
                canvas.DrawImage(backgroundImage, vp.PlotRect, p);
            }
        }

        using (SKPaint gridPaint = new SKPaint { Color = vp.Grid, StrokeWidth = 1, IsAntialias = true })
        using (SKPaint borderPaint = new SKPaint
        {
            Color = ChartViewport.Blend(vp.Background, vp.Foreground, 0.24),
            StrokeWidth = 1f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        })
        using (SKPaint textPaint = new SKPaint
        {
            Color = vp.Foreground,
            IsAntialias = true,
            TextSize = 11,
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        })
        {
            SKRect pr = vp.PlotRect;
            int xDiv = Math.Max(1, vp.GridDivisionsX);
            int yDiv = Math.Max(1, vp.GridDivisionsY);

            for (int i = 0; i <= xDiv; i++)
            {
                float x = pr.Left + pr.Width * i / xDiv;
                canvas.DrawLine(x, pr.Top, x, pr.Bottom, gridPaint);
            }

            for (int i = 0; i <= yDiv; i++)
            {
                float y = pr.Top + pr.Height * i / yDiv;
                canvas.DrawLine(pr.Left, y, pr.Right, y, gridPaint);
            }

            canvas.DrawRect(pr, borderPaint);

            textPaint.TextAlign = SKTextAlign.Right;
            for (int i = 0; i <= yDiv; i++)
            {
                double v = GetYTickValue(vp, i, yDiv);
                float y = pr.Top + pr.Height * i / yDiv;
                string s = FormatTick(v);
                canvas.DrawText(s, pr.Left - 10f, y + 4f, textPaint);
            }

            textPaint.TextAlign = SKTextAlign.Center;
            float tickBaseline = pr.Bottom + 14f;
            for (int i = 0; i <= xDiv; i++)
            {
                double v = GetXTickValue(vp, i, xDiv);
                float x = pr.Left + pr.Width * i / xDiv;
                string s = FormatTick(v);
                canvas.DrawText(s, x, tickBaseline, textPaint);
            }

            textPaint.TextAlign = SKTextAlign.Left;

            if (!string.IsNullOrEmpty(title))
            {
                using (SKPaint titlePaint = new SKPaint
                {
                    Color = vp.Foreground,
                    IsAntialias = true,
                    TextSize = 13,
                    FakeBoldText = true,
                    Typeface = SKTypeface.FromFamilyName("Segoe UI")
                })
                {
                    canvas.DrawText(title, pr.MidX - title.Length * 3.2f, pr.Top - 8, titlePaint);
                }
            }

            if (!string.IsNullOrEmpty(axisX))
            {
                float axisXBaseline = pr.Bottom + 48f;
                float wAxis = textPaint.MeasureText(axisX);
                canvas.DrawText(axisX, pr.MidX - wAxis / 2f, axisXBaseline, textPaint);
            }

            if (!string.IsNullOrEmpty(axisY))
            {
                float maxYLabel = 0f;
                for (int i = 0; i <= yDiv; i++)
                {
                    double v = GetYTickValue(vp, i, yDiv);
                    maxYLabel = Math.Max(maxYLabel, textPaint.MeasureText(FormatTick(v)));
                }

                float labelLeft = pr.Left - 12f - maxYLabel - textPaint.MeasureText(axisY) - 8f;
                canvas.Save();
                canvas.Translate(labelLeft, pr.MidY + textPaint.MeasureText(axisY) / 2f);
                canvas.RotateDegrees(-90);
                canvas.DrawText(axisY, 0, 0, textPaint);
                canvas.Restore();
            }

        }
    }

    /// <summary>
    /// Легенда поверх области графика: подложка и цвета совпадают с <paramref name="legendItems"/> (рисовать после серий).
    /// </summary>
    public static void DrawCartesianLegend(
        SKCanvas canvas,
        ChartViewport vp,
        IReadOnlyList<(string name, SKColor color)> legendItems)
    {
        if (legendItems == null || legendItems.Count == 0)
        {
            return;
        }

        SKRect pr = vp.PlotRect;
        const float pad = 10f;
        const float lineH = 17f;
        const float swatchW = 14f;
        const float swatchH = 9f;
        const float gap = 8f;
        const float cornerR = 6f;
        const float margin = 8f;

        using (SKPaint textPaint = new SKPaint
        {
            Color = vp.Foreground,
            IsAntialias = true,
            TextSize = 11,
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        })
        {
            int rows = 0;
            float maxTextW = 0f;
            foreach ((string name, _) in legendItems)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                rows++;
                maxTextW = Math.Max(maxTextW, textPaint.MeasureText(name));
            }

            if (rows == 0)
            {
                return;
            }

            float innerTextW = Math.Min(maxTextW, pr.Width * 0.42f);
            float boxW = Math.Min(pr.Width * 0.52f, pad * 2 + swatchW + gap + innerTextW + 4f);
            float boxH = pad * 2 + rows * lineH;
            float left = pr.Right - margin - boxW;
            float top = pr.Top + margin;
            SKRect box = new SKRect(left, top, left + boxW, top + boxH);

            // Подложка легенды слегка приподнята от фона к переднему плану (тема-независимо).
            SKColor fillBase = ChartViewport.Blend(vp.Background, vp.Foreground, 0.06);
            SKColor fill = new SKColor(fillBase.Red, fillBase.Green, fillBase.Blue, 235);

            using (SKPaint fillPaint = new SKPaint { Color = fill, IsAntialias = true, Style = SKPaintStyle.Fill })
            using (SKPaint strokePaint = new SKPaint
            {
                Color = ChartViewport.Blend(vp.Background, vp.Foreground, 0.28),
                StrokeWidth = 1f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            })
            {
                canvas.DrawRoundRect(box, cornerR, cornerR, fillPaint);
                canvas.DrawRoundRect(box, cornerR, cornerR, strokePaint);
            }

            float maxLabel = boxW - pad * 2 - swatchW - gap;
            float ly = top + pad + 11f;
            float lx0 = left + pad;
            foreach ((string name, SKColor color) in legendItems)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                string label = FitLegendText(name, textPaint, maxLabel);
                using (SKPaint lp = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true })
                {
                    canvas.DrawRect(lx0, ly - 8f, swatchW, swatchH, lp);
                }

                textPaint.TextAlign = SKTextAlign.Left;
                canvas.DrawText(label, lx0 + swatchW + gap, ly, textPaint);
                ly += lineH;
            }
        }
    }

    private static string FitLegendText(string name, SKPaint paint, float maxWidth)
    {
        if (paint.MeasureText(name) <= maxWidth)
        {
            return name;
        }

        const string ell = "…";
        for (int len = name.Length - 1; len >= 1; len--)
        {
            string t = name.Substring(0, len) + ell;
            if (paint.MeasureText(t) <= maxWidth)
            {
                return t;
            }
        }

        return ell;
    }
}
