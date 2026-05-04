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
    #region Scatter
    /// <summary>
    /// Создание скаттерограммы с данными
    /// </summary>
    public void AddScatter(Vector x, Vector y, string name, Color color)
    {
        ScatterPlot scatter = new ScatterPlot(name);
        scatter.LoadData(x, y);
        scatter.SetColor(Sk(color));
        scatter.AutoSetMarkSize();
        chartElements.Add(scatter);
        AutoScale();
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

    internal void AddElement(IChartElement element)
    {
        chartElements.Add(element);
        AutoScale();
    }

    /// <summary>
    /// Создание скаттерограммы с данными
    /// </summary>
    public void AddScatterMark3(Vector x, Vector y, string name, Color color)
    {
        ScatterPlot scatter = new ScatterPlot(name);
        scatter.LoadData(x, y);
        scatter.SetColor(Sk(color));
        scatter.SetMarkSize(3);
        chartElements.Add(scatter);
        AutoScale();
    }

    /// <summary>
    /// Создание скаттерограммы с данными
    /// </summary>
    public void AddScatterMark6(Vector x, Vector y, string name, Color color)
    {
        ScatterPlot scatter = new ScatterPlot(name);
        scatter.LoadData(x, y);
        scatter.SetColor(Sk(color));
        scatter.SetMarkSize(6);
        chartElements.Add(scatter);
        AutoScale();
    }

    /// <summary>
    /// Создание скаттерограммы с данными
    /// </summary>
    /// <param name="y"></param>
    /// <param name="name"></param>
    public void AddScatterBlack(Vector y, string name = "")
    {
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddScatter(x, y, name, Color.Black);
    }

    /// <summary>
    /// Создание скаттерограммы с данными
    /// </summary>
    public void AddScatterBlack(Vector x, Vector y, string name = "")
    {
        AddScatter(x, y, name, Color.Black);
    }

    /// <summary>
    /// Создание скаттерограммы с данными
    /// </summary>
    /// <param name="y"></param>
    /// <param name="name"></param>
    public void ScatterBlack(Vector y, string name = "")
    {
        Clear();
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddScatter(x, y, name, Color.Black);
    }

    /// <summary>
    /// Создание скаттерограммы с данными
    /// </summary>
    public void ScatterBlack(Vector x, Vector y, string name = "")
    {
        Clear();
        AddScatter(x, y, name, Color.Black);
    }

    /// <summary>
    /// Создание скаттерограммы с данными
    /// </summary>
    /// <param name="y"></param>
    /// <param name="name"></param>
    public void ScatterComplex(ComplexVector y, string name = "")
    {
        Clear();
        Vector x = Vector.SeqBeginsWithZero(1, y.Count);
        AddScatter(x, y.RealVector, name + " [Real]", Color.Blue);
        AddScatter(x, y.ImaginaryVector, name + " [Imaginary]", Color.Red);
    }
    /// <summary>
    /// Создание скаттерограммы отражающей комплексную плоскость
    /// </summary>
    /// <param name="y">Комплексный вектор</param>
    /// <param name="name">Имя</param>
    /// <param name="xScale">Единица измерения шкалы x</param>
    /// <param name="yScale">Единица измерения шкалы y</param>
    public void ScatterComplexPlane(ComplexVector y, string name = "", string xScale = "", string yScale = "")
    {
        Clear();
        ChartName = name;

        LabelX = (xScale != "") ? "Real [" + xScale + "]" : "Real";
        LabelY = (yScale != "") ? "Imaginary [" + yScale + "]" : "Imaginary";

        AddScatter(y.RealVector, y.ImaginaryVector, name, Color.Green);
    }
    /// <summary>
    /// Создание скаттерограммы отражающей комплексную плоскость
    /// </summary>
    /// <param name="y">Комплексный вектор</param>
    /// <param name="name">Имя</param>
    /// <param name="xScale">Единица измерения шкалы x</param>
    /// <param name="yScale">Единица измерения шкалы y</param>
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

        AddScatterMark3(x, y1, "", Color.Black);
        AddScatterMark6(y.RealVector, y.ImaginaryVector, string.Empty, Color.Green);
    }
    #endregion

    #region 3D Charts

    public string LabelZ
    {
        get => _labelZText;
        set { _labelZText = value ?? string.Empty; skChart?.Invalidate(); }
    }

    public Camera3D Camera3D => _camera3D ??= new Camera3D();

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
        skChart?.Invalidate();
    }

    public void AddWireframe(Vector xGrid, Vector yGrid, double[,] z,
        string name, SKColor? color = null, ColormapKind colormap = ColormapKind.Jet)
    {
        var el = new WireframePlot3D(name, xGrid, yGrid, z) { ColormapKind = colormap };
        if (color != null) { el.SetColor(color.Value); el.UseColormap = false; }
        chartElements.Add(el);
        AutoScale3D();
        skChart?.Invalidate();
    }

    public void AddScatter3D(Vector x, Vector y, Vector z,
        string name, SKColor? color = null, ColormapKind colormap = ColormapKind.Jet, float markSize = 4f)
    {
        var el = new ScatterPlot3D(name, x, y, z) { ColormapKind = colormap };
        el.SetMarkSize(markSize);
        if (color != null) { el.SetColor(color.Value); el.UseColormap = false; }
        chartElements.Add(el);
        AutoScale3D();
        skChart?.Invalidate();
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
    }

    private ChartLayoutKind GetCurrentLayout()
    {
        foreach (IChartElement el in chartElements)
            if (el.LayoutKind == ChartLayoutKind.ThreeD) return ChartLayoutKind.ThreeD;
        foreach (IChartElement el in chartElements)
            if (el.LayoutKind == ChartLayoutKind.Pie) return ChartLayoutKind.Pie;
        foreach (IChartElement el in chartElements)
            if (el.LayoutKind == ChartLayoutKind.Polar) return ChartLayoutKind.Polar;
        return ChartLayoutKind.Cartesian;
    }

    private bool Is3DMode => GetCurrentLayout() == ChartLayoutKind.ThreeD;

    #endregion
}
