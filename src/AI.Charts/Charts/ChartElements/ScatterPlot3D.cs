using System;
using System.Collections.Generic;
using AI.Charts.Data;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.Charts.ChartElements;

/// <summary>
/// 3D scatter plot: circles projected into the screen with painter's algorithm.
/// </summary>
[Serializable]
internal sealed class ScatterPlot3D : Base3DChart
{
    private readonly PointCloudData3D _data;
    private float _markSize = 2.5f;

    public ScatterPlot3D(string name, Vector x, Vector y, Vector z) : base(name)
    {
        _data = new PointCloudData3D(x, y, z);
        BoundsXMin = x.Min();
        BoundsXMax = x.Max();
        BoundsYMin = y.Min();
        BoundsYMax = y.Max();
        BoundsZMin = _data.ZMin;
        BoundsZMax = _data.ZMax;
    }

    public void SetMarkSize(float size) => _markSize = Math.Max(1, size);

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        Camera3D cam = vp.Camera3D;
        if (cam == null || _data.Count == 0) return;

        SKRect pr = vp.PlotRect;
        double zRange = BoundsZMax - BoundsZMin;
        if (zRange < 1e-30) zRange = 1;

        var pts = new List<(double depth, int idx)>(_data.Count);
        for (int i = 0; i < _data.Count; i++)
        {
            cam.ProjectToScreen(_data.X[i], _data.Y[i], _data.Z[i], pr, out double depth);
            pts.Add((depth, i));
        }

        pts.Sort((a, b) => b.depth.CompareTo(a.depth));

        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.6f,
            Color = new SKColor(40, 40, 40, 140)
        };

        foreach (var (_, idx) in pts)
        {
            SKPoint sp = cam.ProjectToScreen(_data.X[idx], _data.Y[idx], _data.Z[idx], pr);

            if (UseColormap)
            {
                double t = (_data.Z[idx] - BoundsZMin) / zRange;
                SKColor c = Colormap.Map(t, ColormapKind);
                fillPaint.Color = new SKColor(c.Red, c.Green, c.Blue, 200);
            }
            else
            {
                SKColor c = ElementColor;
                fillPaint.Color = new SKColor(c.Red, c.Green, c.Blue, 200);
            }

            canvas.DrawCircle(sp, _markSize, fillPaint);
            canvas.DrawCircle(sp, _markSize, strokePaint);
        }
    }
}
