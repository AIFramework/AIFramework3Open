using System;
using AI.Charts.Data;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.Charts.ChartElements;

/// <summary>
/// Base class for all 3D chart elements.
/// Stores a 3D bounding box; 2D IData is not used.
/// </summary>
[Serializable]
internal abstract class Base3DChart : IChartElement
{
    public string Name { get; protected set; }
    public SKColor ElementColor { get; protected set; } = SKColors.Gray;
    public int BorderWidth { get; protected set; } = 1;
    public ChartLayoutKind LayoutKind => ChartLayoutKind.ThreeD;

    public ColormapKind ColormapKind { get; set; } = ColormapKind.Jet;
    public bool UseColormap { get; set; } = true;

    protected double BoundsXMin, BoundsXMax;
    protected double BoundsYMin, BoundsYMax;
    protected double BoundsZMin, BoundsZMax;

    IData IChartElement.Data => null;

    protected Base3DChart(string name)
    {
        Name = name ?? string.Empty;
    }

    public void SetColor(SKColor color) => ElementColor = color;

    public void LoadData(Vector x, Vector y) { }
    public void LoadData(IData data) { }
    public void Recalc(double min, double max) { }

    public double GetXMin() => BoundsXMin;
    public double GetXMax() => BoundsXMax;
    public double GetYMin() => BoundsYMin;
    public double GetYMax() => BoundsYMax;
    public double GetZMin() => BoundsZMin;
    public double GetZMax() => BoundsZMax;

    public abstract void Draw(SKCanvas canvas, ChartViewport vp);
}
