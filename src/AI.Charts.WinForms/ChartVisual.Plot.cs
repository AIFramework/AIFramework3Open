using AI.Charts.ChartElements;
using AI.Charts.Data;
using AI.Charts.Forms;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace AI.Charts.WinForms;

public partial class ChartVisual
{
    /// <summary>
    /// Визуализация графиков
    /// </summary>
    /// <param name="chartDatas">Данные графиков</param>
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


    #region Plot

    /// <summary>
    /// Создание графика с данными
    /// </summary>
    public void AddPlot(Vector x, Vector y, string name, Color? color = null, int width = 2, bool isSpline = false)
    {
        Plot plot = new Plot(name) { IsSpline = isSpline };
        plot.LoadData(x, y);
        if (color != null)
        {
            plot.SetColor(Sk(color.Value));
        }
        else
        {
            plot.SetColor(ChartSeriesPalette.Next(ref _plotPaletteIndex));
        }

        plot.SetWidth(width);
        chartElements.Add(plot);
        AutoScale();
    }

    /// <summary>
    /// Создание графика с данными (цвет Skia — как в <see cref="ChartDataSample.ColorChart"/>).
    /// </summary>
    public void AddPlot(Vector x, Vector y, string name, SKColor color, int width = 2, bool isSpline = false)
    {
        Plot plot = new Plot(name) { IsSpline = isSpline };
        plot.LoadData(x, y);
        plot.SetColor(color);
        plot.SetWidth(width);
        chartElements.Add(plot);
        AutoScale();
    }

    /// <summary>
    /// Добавляет кривую со следующим цветом из палитры (не обязательно чёрным).
    /// </summary>
    public void AddPlotBlack(Vector x, Vector y, string name = "", int width = 2, bool isSpline = false)
    {
        AddPlot(x, y, name, null, width, isSpline);
    }

    /// <summary>
    /// Создание графика с данными
    /// </summary>
    public void PlotBlack(Vector x, Vector y, string name = "", int width = 2, bool isSpline = false)
    {
        Clear();
        AddPlot(x, y, name, null, width, isSpline);
    }



    /// <summary>
    /// Создание графика с данными
    /// </summary>
    public void PlotComplex(Vector x, ComplexVector y, string name = "", int width = 2, bool isSpline = false)
    {
        Clear();
        AddPlot(x, y.RealVector, name + " [Real]", Color.Blue, width, isSpline);
        AddPlot(x, y.ImaginaryVector, name + " [Imaginary]", Color.Red, width, isSpline);
    }

    /// <summary>
    /// Создание графика с данными
    /// </summary>
    public void AddPlotBlack(Vector y, string name = "", int width = 2, bool isSpline = false)
    {
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddPlot(x, y, name, null, width, isSpline);
    }

    /// <summary>
    /// Создание графика с данными
    /// </summary>
    public void PlotBlack(Vector y, string name = "", int width = 1, bool isSpline = false)
    {
        Clear();
        _ = skChart.BeginInvoke((MethodInvoker)(() =>
        {
            Vector x = Vector.SeqBeginsWithZero(1, y.Count);
            AddPlot(x, y, name, null, width, isSpline);
        }));
    }

    /// <summary>
    /// Создание графика с данными
    /// </summary>
    public void PlotComplex(ComplexVector y, string name = "", int width = 2, bool isSpline = false)
    {
        Clear();
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddPlot(x, y.RealVector, name + " [Real]", Color.Blue, width, isSpline);
        AddPlot(x, y.ImaginaryVector, name + " [Imaginary]", Color.Red, width, isSpline);
    }

    /// <summary>
	/// Создание графика с данными
	/// </summary>
	public void AddRadialPlot(Vector x, Vector y, string name, Color color, int width = 2)
    {
        RadialPlot radialPlot = new RadialPlot(name);
        radialPlot.LoadData(x, y);
        radialPlot.SetColor(Sk(color));
        radialPlot.SetWidth(width);
        chartElements.Add(radialPlot);
        AutoScale();
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

    /// <summary>
    /// Создание графика с данными
    /// </summary>
    public void AddRadialDegPlot(Vector x, Vector y, string name, Color color, int width = 2)
    {
        AddRadialPlot(x / 180.0 * Math.PI, y, name, color, width);
    }



    /// <summary>
    /// Радиальный график
    /// </summary>
    /// <param name="y"></param>
    /// <param name="name"></param>
    /// <param name="width"></param>
    public void RadPlotBlueDeg(Vector y, string name = "", int width = 1)
    {
        Clear();
        double end = y.Count;
        double step = 360.0 / end;
        Vector x = Vector.SeqBeginsWithZero(step, end);
        x = x.CutAndZero(y.Count);

        AddRadialDegPlot(x, y, name, Color.Blue, width);
    }


    #endregion

    /// <summary>
    /// 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="name"></param>
    public void AddCircul(Vector x, Vector y, string name)
    {
        Circul circul = new Circul(name);
        circul.LoadData(x, y);
        chartElements.Add(circul);
        AutoScale();

    }

    /// <summary>
    /// Добавить закрашенную область
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="name"></param>
    /// <param name="color"></param>
    public void AddArea(Vector x, Vector y, string name, Color color)
    {
        Area area = new Area(name);
        area.LoadData(x, y);
        area.SetColor(Sk(color));
        chartElements.Add(area);
        AutoScale();
    }

    #region Bar
    /// <summary>
	/// Создание гистограммы с данными
	/// </summary>
    public void AddBar(Vector x, Vector y, string name, Color color)
    {
        Bar bar = new Bar(name);
        bar.LoadData(x, y);
        bar.SetColor(Sk(color));
        chartElements.Add(bar);
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

    /// <summary>
    /// Создание гистограммы с данными
    /// </summary>
    public void AddBarBlack(Vector x, Vector y, string name = "")
    {
        AddBar(x, y, name, Color.Black);
    }


    /// <summary>
    /// Создание гистограммы с данными
    /// </summary>
    public void AddBarBlack(Vector y, string name = "")
    {
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddBar(x, y, name, Color.Black);
    }

    /// <summary>
    /// Создание гистограммы с данными
    /// </summary>
    public void BarBlack(Vector x, Vector y, string name = "")
    {
        Clear();
        AddBar(x, y, name, Color.Black);
    }


    /// <summary>
    /// Создание гистограммы с данными
    /// </summary>
    public void BarBlack(Vector y, string name = "")
    {
        Clear();
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddBar(x, y, name, Color.Black);
    }


    #endregion
}
