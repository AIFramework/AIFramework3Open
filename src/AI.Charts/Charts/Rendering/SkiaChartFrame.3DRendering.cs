using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace AI.Charts.Rendering;

internal static partial class SkiaChartFrame
{
    /// <summary>
    /// 3D bounding box frame: 12 edges, axis labels with tick marks, and title.
    /// </summary>
    public static void Draw3DFrame(
        SKCanvas canvas,
        ChartViewport vp,
        string title,
        string axisX,
        string axisY,
        string axisZ,
        ColormapKind colormapKind = ColormapKind.Jet)
    {
        Camera3D cam = vp.Camera3D;
        if (cam == null) return;

        canvas.Clear(vp.Background);
        SKRect pr = vp.PlotRect;

        double x0 = cam.CenterX - cam.Extent;
        double x1 = cam.CenterX + cam.Extent;
        double y0 = cam.CenterY - cam.Extent;
        double y1 = cam.CenterY + cam.Extent;
        double z0 = cam.CenterZ - cam.Extent;
        double z1 = cam.CenterZ + cam.Extent;

        SKPoint[] corners = new SKPoint[8];
        corners[0] = cam.ProjectToScreen(x0, y0, z0, pr);
        corners[1] = cam.ProjectToScreen(x1, y0, z0, pr);
        corners[2] = cam.ProjectToScreen(x1, y1, z0, pr);
        corners[3] = cam.ProjectToScreen(x0, y1, z0, pr);
        corners[4] = cam.ProjectToScreen(x0, y0, z1, pr);
        corners[5] = cam.ProjectToScreen(x1, y0, z1, pr);
        corners[6] = cam.ProjectToScreen(x1, y1, z1, pr);
        corners[7] = cam.ProjectToScreen(x0, y1, z1, pr);

        int[][] edges =
        {
            new[]{0,1}, new[]{1,2}, new[]{2,3}, new[]{3,0},
            new[]{4,5}, new[]{5,6}, new[]{6,7}, new[]{7,4},
            new[]{0,4}, new[]{1,5}, new[]{2,6}, new[]{3,7}
        };

        using var gridPaint = new SKPaint
        {
            Color = new SKColor(
                (byte)(vp.Foreground.Red * 0.3 + vp.Background.Red * 0.7),
                (byte)(vp.Foreground.Green * 0.3 + vp.Background.Green * 0.7),
                (byte)(vp.Foreground.Blue * 0.3 + vp.Background.Blue * 0.7)),
            StrokeWidth = 0.8f,
            IsAntialias = true
        };

        using var textPaint = new SKPaint
        {
            Color = vp.Foreground,
            IsAntialias = true,
            TextSize = 10,
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        };

        foreach (int[] e in edges)
            canvas.DrawLine(corners[e[0]], corners[e[1]], gridPaint);

        const int ticks = 4;

        int xEdgeA, xEdgeB;
        PickBottomEdgeX(corners, out xEdgeA, out xEdgeB);
        DrawAxisTicksAlong(canvas, cam, pr, textPaint, x0, x1, y0, y1, z0, ticks, 0, xEdgeA);

        int yEdgeA, yEdgeB;
        PickBottomEdgeY(corners, out yEdgeA, out yEdgeB);
        DrawAxisTicksAlong(canvas, cam, pr, textPaint, y0, y1, x0, x1, z0, ticks, 1, yEdgeA);

        DrawAxisTicksZ(canvas, cam, pr, textPaint, z0, z1, x0, y0, ticks);

        textPaint.TextSize = 11;
        textPaint.FakeBoldText = true;
        textPaint.TextAlign = SKTextAlign.Center;
        if (!string.IsNullOrEmpty(axisX))
        {
            SKPoint a = corners[xEdgeA], b = corners[xEdgeB];
            float mx = (a.X + b.X) * 0.5f, my = Math.Max(a.Y, b.Y) + 30;
            canvas.DrawText(axisX, mx, my, textPaint);
        }

        if (!string.IsNullOrEmpty(axisY))
        {
            SKPoint a = corners[yEdgeA], b = corners[yEdgeB];
            float mx = (a.X + b.X) * 0.5f, my = Math.Max(a.Y, b.Y) + 30;
            canvas.DrawText(axisY, mx, my, textPaint);
        }

        if (!string.IsNullOrEmpty(axisZ))
        {
            SKPoint lz = cam.ProjectToScreen(x0, y0, (z0 + z1) * 0.5, pr);
            textPaint.TextAlign = SKTextAlign.Right;
            canvas.DrawText(axisZ, lz.X - 20, lz.Y + 4, textPaint);
        }
        textPaint.FakeBoldText = false;

        if (!string.IsNullOrEmpty(title))
        {
            using var titlePaint = new SKPaint
            {
                Color = vp.Foreground,
                IsAntialias = true,
                TextSize = 13,
                FakeBoldText = true,
                Typeface = SKTypeface.FromFamilyName("Segoe UI"),
                TextAlign = SKTextAlign.Center
            };
            canvas.DrawText(title, pr.MidX, 20, titlePaint);
        }

        Draw3DColorbar(canvas, vp, cam, colormapKind);
    }

    private static void PickBottomEdgeX(SKPoint[] c, out int a, out int b)
    {
        float best = float.MinValue;
        a = 0; b = 1;
        int[][] xEdges = { new[]{0,1}, new[]{3,2} };
        foreach (var e in xEdges)
        {
            float my = (c[e[0]].Y + c[e[1]].Y) * 0.5f;
            if (my > best) { best = my; a = e[0]; b = e[1]; }
        }
    }

    private static void PickBottomEdgeY(SKPoint[] c, out int a, out int b)
    {
        float best = float.MinValue;
        a = 0; b = 3;
        int[][] yEdges = { new[]{0,3}, new[]{1,2} };
        foreach (var e in yEdges)
        {
            float my = (c[e[0]].Y + c[e[1]].Y) * 0.5f;
            if (my > best) { best = my; a = e[0]; b = e[1]; }
        }
    }

    private static void DrawAxisTicksAlong(
        SKCanvas canvas, Camera3D cam, SKRect pr, SKPaint textPaint,
        double from, double to, double fixedOtherA, double fixedOtherB,
        double fixedZ, int count, int axis, int edgeCornerIdx)
    {
        textPaint.TextAlign = SKTextAlign.Center;
        double fixedOther = (edgeCornerIdx == 0 || edgeCornerIdx == 1) ? fixedOtherA : fixedOtherB;
        if (axis == 1) fixedOther = (edgeCornerIdx == 0 || edgeCornerIdx == 3) ? fixedOtherA : fixedOtherB;

        for (int i = 0; i <= count; i++)
        {
            double v = from + (to - from) * i / count;
            SKPoint p = axis == 0
                ? cam.ProjectToScreen(v, fixedOther, fixedZ, pr)
                : cam.ProjectToScreen(fixedOther, v, fixedZ, pr);
            canvas.DrawText(FormatTick(v), p.X, p.Y + 16, textPaint);
        }
    }

    private static void DrawAxisTicksZ(
        SKCanvas canvas, Camera3D cam, SKRect pr, SKPaint textPaint,
        double z0, double z1, double fixedX, double fixedY, int count)
    {
        textPaint.TextAlign = SKTextAlign.Right;
        for (int i = 0; i <= count; i++)
        {
            double v = z0 + (z1 - z0) * i / count;
            SKPoint p = cam.ProjectToScreen(fixedX, fixedY, v, pr);
            canvas.DrawText(FormatTick(v), p.X - 8, p.Y + 4, textPaint);
        }
    }

    /// <summary>Vertical colorbar on the right, outside the plot area.</summary>
    internal static void Draw3DColorbar(SKCanvas canvas, ChartViewport vp, Camera3D cam,
        ColormapKind colormapKind = ColormapKind.Jet)
    {
        float totalW = vp.Width;
        float barW = 16f;
        float barH = vp.PlotRect.Height * 0.55f;
        float barL = totalW - 52f;
        float barT = vp.PlotRect.MidY - barH * 0.5f;

        double zMin = cam.CenterZ - cam.Extent;
        double zMax = cam.CenterZ + cam.Extent;

        int steps = Math.Max(2, (int)barH);
        for (int i = 0; i < steps; i++)
        {
            float yy = barT + barH - i * barH / steps;
            double t = (double)i / steps;
            SKColor c = Colormap.Map(t, colormapKind);
            using var p = new SKPaint { Color = c };
            canvas.DrawRect(barL, yy, barW, barH / steps + 1f, p);
        }

        using var borderP = new SKPaint
        {
            Color = vp.Foreground,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawRect(barL, barT, barW, barH, borderP);

        using var tp = new SKPaint
        {
            Color = vp.Foreground,
            TextSize = 10,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI"),
            TextAlign = SKTextAlign.Left
        };
        canvas.DrawText(FormatTick(zMax), barL + barW + 4, barT + 6, tp);
        canvas.DrawText(FormatTick(zMin), barL + barW + 4, barT + barH + 2, tp);
    }
}
