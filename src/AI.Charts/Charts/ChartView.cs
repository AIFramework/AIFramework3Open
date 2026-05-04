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

/// <summary>
/// Кроссплатформенное построение графиков (SkiaSharp), без привязки к UI-фреймворку.
/// </summary>
[Serializable]
public sealed partial class ChartView
{
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
    private int _plotPaletteIndex;
    private Camera3D _camera3D;

    public string ChartName
    {
        get => _chartTitle;
        set => _chartTitle = value ?? string.Empty;
    }

    public string LabelX
    {
        get => _labelXText;
        set => _labelXText = value ?? string.Empty;
    }

    public string LabelY
    {
        get => _labelYText;
        set => _labelYText = value ?? string.Empty;
    }

    public string LabelZ
    {
        get => _labelZText;
        set => _labelZText = value ?? string.Empty;
    }

    /// <summary>Camera for 3D chart types. Lazy-initialized on first access.</summary>
    public Camera3D Camera3D => _camera3D ??= new Camera3D();

    public bool IsLogScale
    {
        get => _axisLogY;
        set => _axisLogY = value;
    }

    /// <summary>Цвет фона области графика.</summary>
    public SKColor BackgroundColor { get; set; } = SKColors.White;

    /// <summary>Цвет осей и подписей.</summary>
    public SKColor ForegroundColor { get; set; } = SKColors.Black;

    public void SetBackgroundImage(SKImage image)
    {
        _backgroundSkImage?.Dispose();
        _backgroundSkImage = image;
    }

    /// <summary>Рисует график на холст Skia (размер в пикселях).</summary>
    public void Draw(SKCanvas canvas, float width, float height)
    {
        SKImageInfo info = new SKImageInfo((int)Math.Max(1, width), (int)Math.Max(1, height));
        RenderChart(canvas, info);
    }

    /// <summary>Создаёт растровое изображение графика.</summary>
    public SKBitmap ToBitmap(int width, int height)
    {
        int w = Math.Max(1, width);
        int h = Math.Max(1, height);
        SKBitmap bmp = new SKBitmap(w, h);
        using (SKCanvas c = new SKCanvas(bmp))
        {
            RenderChart(c, bmp.Info);
        }

        return bmp;
    }

    public void Clear()
    {
        chartElements.Clear();
        _plotPaletteIndex = 0;
        _axisXMin = 0;
        _axisXMax = 1;
        _axisYMin = 0;
        _axisYMax = 1;
        _camera3D = null;
    }

    /// <summary>Текущий тип компоновки (декартова / полярная / круговая).</summary>
    public ChartLayoutKind CurrentLayout =>
        chartElements.Count == 0 ? ChartLayoutKind.Cartesian : GetCurrentLayout();

    /// <summary>Есть ли данные для отрисовки.</summary>
    public bool HasRenderableData()
    {
        foreach (IChartElement el in chartElements)
        {
            if (el is Base3DChart) return true;
            if (el.Data != null && el.Data.Count > 0) return true;
        }

        return false;
    }

    /// <summary>Пиксель окна отображения -> значения на осях (или полярные).</summary>
    public void MapPixelToValue(float mx, float my, float width, float height, out double xv, out double yv)
    {
        ChartViewport vp = BuildViewport(width, height);
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

    public double PixelToValueX(int xPixel, float width, float height) =>
        BuildViewport(width, height).PxToX(xPixel);

    public double PixelToValueY(int yPixel, float width, float height) =>
        BuildViewport(width, height).PxToY(yPixel);

    public void SetAxisRange(double xMin, double xMax, double yMin, double yMax) =>
        SetScale(xMin, xMax, yMin, yMax);

    public void SetAxisRangeX(double xMin, double xMax)
    {
        _axisXMin = xMin;
        _axisXMax = xMax;
    }

    public void SetAxisRangeY(double yMin, double yMax)
    {
        _axisYMin = yMin;
        _axisYMax = yMax;
    }

    /// <summary>Пересчёт серий при изменении окна по X (как в WinForms).</summary>
    public void RecalcForAxisWindow()
    {
        Rec();
    }

    public int SeriesCount => chartElements.Count;

    public bool TryGetFirstXStep(out double step)
    {
        step = 0;
        if (chartElements.Count == 0)
        {
            return false;
        }

        IData d = chartElements[0].Data;
        if (d == null || d.Count < 2)
        {
            return false;
        }

        Vector vx = d.GetX();
        step = vx[1] - vx[0];
        return true;
    }

    public double AxisXMin => _axisXMin;
    public double AxisXMax => _axisXMax;
    public double AxisYMin => _axisYMin;
    public double AxisYMax => _axisYMax;

    /// <summary>Доступ к сериям для хостов UI (WinForms и др.).</summary>
    internal IReadOnlyList<IChartElement> ChartElements => chartElements;

    /// <summary>True if a background image has been set (decision boundary, heatmap, etc.).</summary>
    public bool HasBackgroundImage => _backgroundSkImage != null;

    public void AutoScale()
    {
        ScaleData scale = chartElements.GetEnumerator().GetScaleData();
        double xMin = scale.MinX, xMax = scale.MaxX, yMin = scale.MinY, yMax = scale.MaxY, yMin2, yMax2;

        if (IsLogScale)
        {
            if (yMin == 0)
            {
                throw new Exception("При использовании логарифмического масштаба, значение 0 не допустимо");
            }

            if (yMin < 0)
            {
                throw new Exception("При использовании логарифмического масштаба, значения ниже нуля не допустимы");
            }
        }

        double dY = Math.Abs(yMax - yMin);
        yMin2 = yMin - (0.2 * dY);
        yMax2 = yMax + (dY * 0.2);

        if (IsLogScale)
        {
            yMax2 = (yMax2 > 0) ? yMax : 1e-200;
            yMin2 = (yMin2 > 0) ? yMin2 : 1e-200;
        }

        if (yMin2 == yMax2)
        {
            yMax2 = 1;
        }

        SetScale(xMin, xMax, yMin2, yMax2);
        RecalcElements();
    }

    private void Rec()
    {
        double min = MinX(), max = MaxX();
        foreach (IChartElement item in chartElements)
        {
            item.Recalc(min, max);
        }
    }

    private void RecalcElements()
    {
        Rec();
    }

    private void SetScale(double xMin, double xMax, double yMin, double yMax)
    {
        _axisXMin = xMin;
        _axisXMax = xMax;
        _axisYMin = yMin;
        _axisYMax = yMax;
    }

    private double MinX() => _axisXMin;
    private double MaxX() => _axisXMax;
    private double MinY() => _axisYMin;
    private double MaxY() => _axisYMax;
}
