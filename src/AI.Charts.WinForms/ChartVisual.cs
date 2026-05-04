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

/// <summary>
/// Визуально представление данных (Графики)
/// </summary>
[Serializable]
public partial class ChartVisual : UserControl
{
    private static SKColor Sk(Color c) => new SKColor(c.R, c.G, c.B, c.A);

    private int _plotPaletteIndex;

    #region Свойства

    /// <summary>
    /// Можно ли перемещать график
    /// </summary>
    public bool IsMoove { get; set; } = true;

    /// <summary>
    /// Можно ли масштабировать
    /// </summary>
    public bool IsScale { get; set; } = true;


    /// <summary>
    /// Выводить ли значения x,y
    /// </summary>
    public bool IsShowXY { get; set; } = true;


    /// <summary>
    /// Использовать ли контекстное меню
    /// </summary>
    public bool IsContextMenu
    {
        get => skChart.ContextMenuStrip == contextMenu;

        set
        {
            if (value)
            {
                skChart.ContextMenuStrip = contextMenu;
            }
            else
            {
                skChart.ContextMenuStrip = null;
            }
        }
    }

    /// <summary>
    /// Имя графика
    /// </summary>
    public string ChartName
    {
        get => _chartTitle;
        set
        {
            _chartTitle = value ?? string.Empty;
            skChart?.Invalidate();
        }
    }

    /// <summary>
    /// Имя оси X
    /// </summary>
    public string LabelX
    {
        get => _labelXText;
        set
        {
            _labelXText = value ?? string.Empty;
            skChart?.Invalidate();
        }
    }

    /// <summary>
    /// Имя оси Y
    /// </summary>
    public string LabelY
    {
        get => _labelYText;
        set
        {
            _labelYText = value ?? string.Empty;
            skChart?.Invalidate();
        }
    }

    /// <summary>
    /// График в логарифмическом масштабе
    /// </summary>
    public bool IsLogScale
    {
        get => _axisLogY;
        set
        {
            _axisLogY = value;
            skChart?.Invalidate();
        }
    }


    private readonly List<IChartElement> chartElements = new List<IChartElement>();

    private double _axisXMin;
    private double _axisXMax = 1;
    private double _axisYMin;
    private double _axisYMax = 1;
    private string _chartTitle = "График";
    private string _labelXText = "Ось Х";
    private string _labelYText = "Ось Y";
    private string _labelZText = "Ось Z";
    private bool _axisLogY;
    private SKImage _backgroundSkImage;
    private Camera3D _camera3D;
    private bool _drag3D;
    private System.Drawing.Point _drag3DLast;

    #endregion



    /// <summary>
    /// Графики
    /// </summary>
    public ChartVisual()
    {
        InitializeComponent();
        labelXY.BringToFront();
        Clear();
    }



    /// <summary>
    ///  Отрисовка графика
    /// </summary>
    public Bitmap ChartImg()
    {
        using (SKBitmap bmp = new SKBitmap(Math.Max(1, skChart.Width), Math.Max(1, skChart.Height)))
        {
            using (SKCanvas c = new SKCanvas(bmp))
            {
                RenderChart(c, bmp.Info);
            }

            return bmp.ToBitmap();
        }
    }


    /// <summary>
    /// Очистка графика
    /// </summary>
    public void Clear()
    {
        if (skChart.InvokeRequired)
            _ = skChart.Invoke(new MethodInvoker(ClearInvoked));

        else ClearInvoked();

    }



    #region Invoked
    private void ClearInvoked()
    {
        chartElements.Clear();
        _plotPaletteIndex = 0;
        _axisXMin = 0;
        _axisXMax = 1;
        _axisYMin = 0;
        _axisYMax = 1;
        skChart.Invalidate();
    }

    #endregion
}
