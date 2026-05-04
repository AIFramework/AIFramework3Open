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
    public void VisualData(ChartData chartDatas)
    {
        LabelX = chartDatas[0].DescriptionData.X;
        LabelY = chartDatas[0].DescriptionData.Y;
        ChartName = chartDatas.ChartName;

        for (int i = 0; i < chartDatas.Count; i++)
        {
            switch (chartDatas[i].ChartType)
            {
                case ChartType.Plot:
                    AddPlot(chartDatas[i].DataX, chartDatas[i].DataY, chartDatas[i].DescriptionData.Name, chartDatas[i].ColorChart, 2);
                    break;
                case ChartType.Bar:
                    AddBar(chartDatas[i].DataX, chartDatas[i].DataY, chartDatas[i].DescriptionData.Name, chartDatas[i].ColorChart);
                    break;
                case ChartType.Spline:
                    AddPlot(chartDatas[i].DataX, chartDatas[i].DataY, chartDatas[i].DescriptionData.Name, chartDatas[i].ColorChart, 2, true);
                    break;
                case ChartType.Scatter:
                    AddScatter(chartDatas[i].DataX, chartDatas[i].DataY, chartDatas[i].DescriptionData.Name, chartDatas[i].ColorChart);
                    break;
            }
        }
    }

    public void AddPlot(Vector x, Vector y, string name, SKColor? color = null, int width = 2, bool isSpline = false)
    {
        Plot plot = new Plot(name) { IsSpline = isSpline };
        plot.LoadData(x, y);
        if (color != null)
        {
            plot.SetColor(color.Value);
        }
        else
        {
            plot.SetColor(ChartSeriesPalette.Next(ref _plotPaletteIndex));
        }

        plot.SetWidth(width);
        chartElements.Add(plot);
        AutoScale();
    }

    /// <summary>Добавляет кривую со следующим цветом из палитры (не обязательно чёрным).</summary>
    public void AddPlotBlack(Vector x, Vector y, string name = "", int width = 2, bool isSpline = false)
    {
        AddPlot(x, y, name, null, width, isSpline);
    }

    public void PlotBlack(Vector x, Vector y, string name = "", int width = 2, bool isSpline = false)
    {
        Clear();
        AddPlot(x, y, name, null, width, isSpline);
    }

    public void PlotBlack(Vector y, string name = "", int width = 1, bool isSpline = false)
    {
        Clear();
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddPlot(x, y, name, null, width, isSpline);
    }

    public void PlotComplex(Vector x, ComplexVector y, string name = "", int width = 2, bool isSpline = false)
    {
        Clear();
        AddPlot(x, y.RealVector, name + " [Real]", SKColors.Blue, width, isSpline);
        AddPlot(x, y.ImaginaryVector, name + " [Imaginary]", SKColors.Red, width, isSpline);
    }

    public void PlotComplex(ComplexVector y, string name = "", int width = 2, bool isSpline = false)
    {
        Clear();
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        PlotComplex(x, y, name, width, isSpline);
    }

    public void AddRadialPlot(Vector x, Vector y, string name, SKColor color, int width = 2)
    {
        RadialPlot radialPlot = new RadialPlot(name);
        radialPlot.LoadData(x, y);
        radialPlot.SetColor(color);
        radialPlot.SetWidth(width);
        chartElements.Add(radialPlot);
        AutoScale();
    }

    public void AddRadialDegPlot(Vector x, Vector y, string name, SKColor color, int width = 2)
    {
        AddRadialPlot(x / 180.0 * Math.PI, y, name, color, width);
    }

    public void RadPlotBlueDeg(Vector y, string name = "", int width = 1)
    {
        Clear();
        double end = y.Count;
        double step = 360.0 / end;
        Vector x = Vector.SeqBeginsWithZero(step, end);
        x = x.CutAndZero(y.Count);
        AddRadialDegPlot(x, y, name, SKColors.Blue, width);
    }

    public void AddCircul(Vector x, Vector y, string name)
    {
        Circul circul = new Circul(name);
        circul.LoadData(x, y);
        chartElements.Add(circul);
        AutoScale();
    }

    public void AddArea(Vector x, Vector y, string name, SKColor color)
    {
        Area area = new Area(name);
        area.LoadData(x, y);
        area.SetColor(color);
        chartElements.Add(area);
        AutoScale();
    }

    public void AddBar(Vector x, Vector y, string name, SKColor color)
    {
        Bar bar = new Bar(name);
        bar.LoadData(x, y);
        bar.SetColor(color);
        chartElements.Add(bar);
        AutoScale();
    }

    public void AddBarBlack(Vector x, Vector y, string name = "")
    {
        AddBar(x, y, name, SKColors.Black);
    }

    public void AddBarBlack(Vector y, string name = "")
    {
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddBar(x, y, name, SKColors.Black);
    }

    public void BarBlack(Vector x, Vector y, string name = "")
    {
        Clear();
        AddBar(x, y, name, SKColors.Black);
    }

    public void BarBlack(Vector y, string name = "")
    {
        Clear();
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddBar(x, y, name, SKColors.Black);
    }

    public void AddScatter(Vector x, Vector y, string name, SKColor color)
    {
        ScatterPlot scatter = new ScatterPlot(name);
        scatter.LoadData(x, y);
        scatter.SetColor(color);
        scatter.AutoSetMarkSize();
        chartElements.Add(scatter);
        AutoScale();
    }

    /// <summary>
    /// Добавляет визуализацию направленного графа (DAG, дерево задач, конечный автомат).
    /// </summary>
    public void AddGraph(GraphData graph, string name = "Graph")
    {
        var gc = new GraphChart(name, graph);
        chartElements.Add(gc);
        AutoScale();
    }

    internal void AddElement(IChartElement element)
    {
        chartElements.Add(element);
        AutoScale();
    }

    public void AddScatterMark3(Vector x, Vector y, string name, SKColor color)
    {
        ScatterPlot scatter = new ScatterPlot(name);
        scatter.LoadData(x, y);
        scatter.SetColor(color);
        scatter.SetMarkSize(3);
        chartElements.Add(scatter);
        AutoScale();
    }

    public void AddScatterMark6(Vector x, Vector y, string name, SKColor color)
    {
        ScatterPlot scatter = new ScatterPlot(name);
        scatter.LoadData(x, y);
        scatter.SetColor(color);
        scatter.SetMarkSize(6);
        chartElements.Add(scatter);
        AutoScale();
    }

    public void AddScatterBlack(Vector y, string name = "")
    {
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddScatter(x, y, name, SKColors.Black);
    }

    public void AddScatterBlack(Vector x, Vector y, string name = "")
    {
        AddScatter(x, y, name, SKColors.Black);
    }

    public void ScatterBlack(Vector y, string name = "")
    {
        Clear();
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddScatter(x, y, name, SKColors.Black);
    }

    public void ScatterBlack(Vector x, Vector y, string name = "")
    {
        Clear();
        AddScatter(x, y, name, SKColors.Black);
    }

    public void ScatterComplex(ComplexVector y, string name = "")
    {
        Clear();
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddScatter(x, y.RealVector, name + " [Real]", SKColors.Blue);
        AddScatter(x, y.ImaginaryVector, name + " [Imaginary]", SKColors.Red);
    }

    public void ScatterComplexPlane(ComplexVector y, string name = "", string xScale = "", string yScale = "")
    {
        Clear();
        ChartName = name;
        LabelX = (xScale != "") ? "Real [" + xScale + "]" : "Real";
        LabelY = (yScale != "") ? "Imaginary [" + yScale + "]" : "Imaginary";
        AddScatter(y.RealVector, y.ImaginaryVector, name, SKColors.Green);
    }

    public void ScatterComplexPlaneWithRing1(ComplexVector y, string name = "", string xScale = "", string yScale = "")
    {
        Clear();
        ChartName = name;
        LabelX = (xScale != "") ? "Real [" + xScale + "]" : "Real";
        LabelY = (yScale != "") ? "Imaginary [" + yScale + "]" : "Imaginary";

        Vector x = Vector.Seq(-1, 0.001, 1);
        x = x.InterpolayrZero(2);
        Vector y1 = new Vector(x.Count);

        for (int i = 0; i < x.Count; i += 2)
        {
            y1[i] = Math.Sqrt(1 - (x[i] * x[i]));
            y1[i + 1] = -y1[i];
        }

        AddScatterMark3(x, y1, "", SKColors.Black);
        AddScatterMark6(y.RealVector, y.ImaginaryVector, string.Empty, SKColors.Green);
    }

    public void AddPlot(Vector y, string name, SKColor color, int width = 2, bool isSpline = false)
    {
        AddPlot(Vector.SeqBeginsWithZero(1, y.Count), y, name, color, width, isSpline);
    }

    public void AddSpectrum(Vector x, Vector y, SKColor color, string name)
    {
        double dt = Statistic.MeanStep2(x);
        Vector magn = FFT.CalcFFT(y * WindowForFFT.HammingWindow(y.Count)).MagnitudeVector;
        Vector f = Signal.Frequency(magn.Count, 1.0 / dt);
        magn = magn.CutAndZero(magn.Count / 2);
        magn /= magn.Count;
        AddPlot(f.CutAndZero(f.Count / 2), magn, name, color);
    }

    public void AddDiff(Vector x, Vector y, SKColor color, string name, int w)
    {
        double dt = Statistic.MeanStep2(x);
        AddPlot(x, Functions.Diff(y, 1.0 / dt), name, color, w);
    }

    public void AddIntegr(Vector x, Vector y, SKColor color, string name, int w)
    {
        double dt = Statistic.MeanStep(x);
        AddPlot(x, Functions.Integral(y, 1.0 / dt), name, color, w);
    }

    public void AddHistoramm(Vector y, SKColor color, string name)
    {
        Statistic statistic = new Statistic(y);
        Histogramm histogramm = statistic.Histogramm((int)(2 * Math.Log(y.Count, 2)));
        AddArea(histogramm.X, histogramm.Y, name, color);
    }
}
