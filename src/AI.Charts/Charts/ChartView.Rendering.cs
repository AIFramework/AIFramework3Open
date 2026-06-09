using AI.Charts.ChartElements;
using AI.Charts.Data;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.DSP.DSPCore;
using AI.Statistics;
using SkiaSharp;
using System;
using System.Collections.Generic;
namespace AI.Charts;

public sealed partial class ChartView
{
    #region 3D charts

    /// <summary>Filled surface plot colored by Z value.</summary>
    public void AddSurface(Vector xGrid, Vector yGrid, double[,] z,
        string name, ColormapKind colormap = ColormapKind.Jet, bool showEdges = true)
    {
        var el = new SurfacePlot3D(name, xGrid, yGrid, z)
        {
            ColormapKind = colormap,
            ShowEdges = showEdges
        };
        chartElements.Add(el);
        AutoScale3D();
    }

    /// <summary>Wireframe-only surface plot.</summary>
    public void AddWireframe(Vector xGrid, Vector yGrid, double[,] z,
        string name, SKColor? color = null, ColormapKind colormap = ColormapKind.Jet)
    {
        var el = new WireframePlot3D(name, xGrid, yGrid, z) { ColormapKind = colormap };
        if (color != null)
        {
            el.SetColor(color.Value);
            el.UseColormap = false;
        }
        chartElements.Add(el);
        AutoScale3D();
    }

    /// <summary>3D scatter plot (point cloud).</summary>
    public void AddScatter3D(Vector x, Vector y, Vector z,
        string name, SKColor? color = null, ColormapKind colormap = ColormapKind.Jet, float markSize = 4f)
    {
        var el = new ScatterPlot3D(name, x, y, z) { ColormapKind = colormap };
        el.SetMarkSize(markSize);
        if (color != null)
        {
            el.SetColor(color.Value);
            el.UseColormap = false;
        }
        chartElements.Add(el);
        AutoScale3D();
    }

    private void AutoScale3D()
    {
        double xMin = double.MaxValue, xMax = double.MinValue;
        double yMin = double.MaxValue, yMax = double.MinValue;
        double zMin = double.MaxValue, zMax = double.MinValue;
        bool any = false;

        foreach (IChartElement el in chartElements)
        {
            if (el is Base3DChart el3)
            {
                any = true;
                xMin = Math.Min(xMin, el3.GetXMin());
                xMax = Math.Max(xMax, el3.GetXMax());
                yMin = Math.Min(yMin, el3.GetYMin());
                yMax = Math.Max(yMax, el3.GetYMax());
                zMin = Math.Min(zMin, el3.GetZMin());
                zMax = Math.Max(zMax, el3.GetZMax());
            }
        }

        if (!any) return;

        Camera3D.FitToBounds(xMin, xMax, yMin, yMax, zMin, zMax);
        _axisXMin = xMin;
        _axisXMax = xMax;
        _axisYMin = yMin;
        _axisYMax = yMax;
    }

    internal void AddChartElement(IChartElement element)
    {
        if (element is Plot plot)
        {
            AddPlot(
                plot.Data.GetX(),
                plot.Data.GetY(),
                plot.Name,
                plot.ElementColor,
                plot.BorderWidth,
                plot.IsSpline);
        }
        else if (element is Bar bar)
        {
            AddBar(
                bar.Data.GetX(),
                bar.Data.GetY(),
                bar.Name,
                bar.ElementColor);
        }
        else if (element is ScatterPlot scatter)
        {
            AddScatter(
                scatter.Data.GetX(),
                scatter.Data.GetY(),
                scatter.Name,
                scatter.ElementColor);
        }
        else if (element is RadialPlot radial)
        {
            AddRadialPlot(
                radial.Data.GetX(),
                radial.Data.GetY(),
                radial.Name,
                radial.ElementColor);
        }
    }

    #endregion 3D charts

    /// <summary>
    /// Exports all chart series as simple DTOs for external consumers (e.g. Plotly.js).
    /// </summary>
    public ChartExport Export()
    {
        string layoutKind = "cartesian";
        var series = new List<ChartSeriesExport>();
        GraphData graphData = null;

        foreach (var el in chartElements)
        {
            if (el is GraphChart gc)
            {
                layoutKind = "graph";
                graphData = gc.Graph;
                continue;
            }

            string type;
            if (el is Plot p)
            {
                type = p.IsSpline ? "spline" : "line";
            }
            else if (el is Bar)
                type = "bar";
            else if (el is ScatterPlot)
                type = "scatter";
            else if (el is Area)
                type = "area";
            else if (el is RadialPlot)
            {
                type = "polar";
                layoutKind = "polar";
            }
            else if (el is Circul)
            {
                type = "pie";
                layoutKind = "pie";
            }
            else if (el is Base3DChart)
            {
                layoutKind = "3d";
                continue;
            }
            else
                type = "line";

            var data = el.Data;
            if (data == null || data.Count == 0) continue;
            var xVec = data.GetX();
            var yVec = data.GetY();
            int n = Math.Min(xVec.Count, yVec.Count);
            var xArr = new double[n];
            var yArr = new double[n];
            for (int i = 0; i < n; i++) { xArr[i] = xVec[i]; yArr[i] = yVec[i]; }

            var c = el.ElementColor;
            series.Add(new ChartSeriesExport(el.Name, type, xArr, yArr,
                c.Red, c.Green, c.Blue, c.Alpha, el.BorderWidth));
        }

        return new ChartExport(
            _chartTitle, _labelXText, _labelYText,
            _axisLogY, _backgroundSkImage != null,
            layoutKind, series, graphData);
    }

    private ChartLayoutKind GetCurrentLayout()
    {
        foreach (IChartElement el in chartElements)
        {
            if (el.LayoutKind == ChartLayoutKind.ThreeD)
            {
                return ChartLayoutKind.ThreeD;
            }
        }

        foreach (IChartElement el in chartElements)
        {
            if (el.LayoutKind == ChartLayoutKind.Pie)
            {
                return ChartLayoutKind.Pie;
            }
        }

        foreach (IChartElement el in chartElements)
        {
            if (el.LayoutKind == ChartLayoutKind.Polar)
            {
                return ChartLayoutKind.Polar;
            }
        }

        return ChartLayoutKind.Cartesian;
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
            Foreground = ForegroundColor,
            Background = BackgroundColor,
            // Тон сетки подбирается от фона к переднему плану — мягкая сетка
            // одинаково аккуратна и на светлой, и на тёмной теме.
            Grid = ChartViewport.Blend(BackgroundColor, ForegroundColor, 0.16)
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

    private void RenderChart(SKCanvas canvas, SKImageInfo info)
    {
        float w = info.Width;
        float h = info.Height;
        ChartLayoutKind layout = chartElements.Count == 0 ? ChartLayoutKind.Cartesian : GetCurrentLayout();
        ChartViewport vp = BuildViewport(w, h);

        canvas.Clear(BackgroundColor);

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
}
