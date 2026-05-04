using AI.Charts.ChartElements;
using AI.Charts.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AI.Charts.WinForms;

public partial class ChartVisual
{
    private void SkChart_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        SKImageInfo info = e.Info;
        SKSurface surface = e.Surface;
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(ToSkColor(BackColor));
        RenderChart(canvas, info);
        if (IsScale)
        {
            int rx = xMouseB > xMouseE ? xMouseE : xMouseB;
            int ry = yMouseB > yMouseE ? yMouseE : yMouseB;
            int rw = Math.Abs(xMouseB - xMouseE);
            int rh = Math.Abs(yMouseB - yMouseE);
            if (rw > 0 && rh > 0)
            {
                using (SKPaint pen = new SKPaint { Color = SKColors.Red, StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true })
                {
                    canvas.DrawRect(rx, ry, rw, rh, pen);
                }
            }
        }
    }

    private static SKColor ToSkColor(Color c)
    {
        return new SKColor(c.R, c.G, c.B, c.A);
    }

    internal void RenderChart(SKCanvas canvas, SKImageInfo info)
    {
        if (skChart == null)
        {
            return;
        }

        float w = info.Width;
        float h = info.Height;
        ChartLayoutKind layout = chartElements.Count == 0 ? ChartLayoutKind.Cartesian : GetCurrentLayout();
        ChartViewport vp = BuildViewport(w, h);

        if (layout == ChartLayoutKind.Cartesian)
        {
            List<(string name, SKColor color)> legend = new List<(string, SKColor)>();
            foreach (IChartElement el in chartElements)
            {
                if (!string.IsNullOrEmpty(el.Name))
                {
                    legend.Add((el.Name, el.ElementColor));
                }
            }

            SkiaChartFrame.DrawCartesianFrame(canvas, vp, _chartTitle, _labelXText, _labelYText, _backgroundSkImage);
            canvas.Save();
            canvas.ClipRect(vp.PlotRect, SKClipOperation.Intersect, antialias: true);
            for (int i = chartElements.Count - 1; i >= 0; i--)
            {
                chartElements[i].Draw(canvas, vp);
            }

            canvas.Restore();
            SkiaChartFrame.DrawCartesianLegend(canvas, vp, legend);
        }
        else if (layout == ChartLayoutKind.Polar)
        {
            SkiaChartFrame.DrawPolarFrame(canvas, vp, _chartTitle, _labelYText);
            foreach (IChartElement el in chartElements)
            {
                if (el is RadialPlot)
                {
                    el.Draw(canvas, vp);
                }
            }
        }
        else if (layout == ChartLayoutKind.Pie)
        {
            SkiaChartFrame.DrawPieFrame(canvas, vp, _chartTitle);
            foreach (IChartElement el in chartElements)
            {
                if (el is Circul)
                {
                    el.Draw(canvas, vp);
                }
            }
        }
        else if (layout == ChartLayoutKind.ThreeD)
        {
            ColormapKind cmk = ColormapKind.Jet;
            foreach (IChartElement el in chartElements)
                if (el is Base3DChart b3 && b3.UseColormap) { cmk = b3.ColormapKind; break; }

            SkiaChartFrame.Draw3DFrame(canvas, vp, _chartTitle, _labelXText, _labelYText, _labelZText, cmk);
            foreach (IChartElement el in chartElements)
            {
                if (el is Base3DChart)
                {
                    el.Draw(canvas, vp);
                }
            }
        }
    }

    private ChartViewport BuildViewport(float w, float h)
    {
        ChartViewport vp = new ChartViewport
        {
            Width = w,
            Height = h,
            XMin = _axisXMin,
            XMax = _axisXMax,
            YMin = _axisYMin,
            YMax = _axisYMax,
            LogY = _axisLogY,
            Foreground = ToSkColor(ForeColor),
            Background = ToSkColor(BackColor),
            Grid = new SKColor(180, 180, 180)
        };

        ChartLayoutKind layout = GetCurrentLayout();
        if (layout == ChartLayoutKind.ThreeD)
        {
            const float ml = 30f, mr = 64f, mt = 30f, mb = 30f;
            vp.PlotRect = new SKRect(ml, mt, w - mr, h - mb);
            vp.Camera3D = _camera3D ?? (_camera3D = new Camera3D());
        }
        else if (layout == ChartLayoutKind.Cartesian)
        {
            SkiaChartFrame.LayoutCartesianMargins(vp, w, h, _labelYText, _labelXText);
        }
        else
        {
            vp.GridDivisionsX = 8;
            vp.GridDivisionsY = 8;
            const float ml = 56f;
            const float mr = 14f;
            const float mt = 36f;
            const float mb = 46f;
            vp.PlotRect = new SKRect(ml, mt, w - mr, h - mb);
        }

        if (layout == ChartLayoutKind.Polar)
        {
            float pr = Math.Min(vp.PlotRect.Width, vp.PlotRect.Height) * 0.45f;
            vp.PolarCx = vp.PlotRect.MidX;
            vp.PolarCy = vp.PlotRect.MidY;
            vp.PolarRingRadiusPx = pr;
            vp.PolarDataRadiusMax = Math.Max(_axisYMax, 1e-30);
        }
        else if (layout == ChartLayoutKind.Pie)
        {
            float pr = Math.Min(vp.PlotRect.Width, vp.PlotRect.Height) * 0.42f;
            vp.PieCx = vp.PlotRect.MidX;
            vp.PieCy = vp.PlotRect.MidY;
            vp.PieRadius = pr;
        }

        return vp;
    }

    private void TryMapMouseToValues(float mx, float my, out double xv, out double yv)
    {
        ChartViewport vp = BuildViewport(skChart.Width, skChart.Height);
        ChartLayoutKind layout = GetCurrentLayout();
        if (layout == ChartLayoutKind.Cartesian)
        {
            SKRect pr = vp.PlotRect;
            float mxc = Math.Max(pr.Left, Math.Min(mx, pr.Right));
            float myc = Math.Max(pr.Top, Math.Min(my, pr.Bottom));
            xv = vp.PxToX(mxc);
            yv = vp.PxToY(myc);
            return;
        }

        if (layout == ChartLayoutKind.Polar)
        {
            double dx = mx - vp.PolarCx;
            double dy = my - vp.PolarCy;
            xv = Math.Atan2(-dy, dx);
            if (xv < 0)
            {
                xv += 2 * Math.PI;
            }

            double dist = Math.Sqrt(dx * dx + dy * dy);
            double rmax = vp.PolarDataRadiusMax > 1e-30 ? vp.PolarDataRadiusMax : 1;
            yv = dist / Math.Max(vp.PolarRingRadiusPx, 1e-3) * rmax;
            return;
        }

        xv = 0;
        yv = 0;
    }

    private bool HasRenderableData()
    {
        foreach (IChartElement el in chartElements)
        {
            if (el is Base3DChart) return true;
            if (el.Data != null && el.Data.Count > 0) return true;
        }

        return false;
    }
}
