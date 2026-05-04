using System;
using System.Collections.Generic;
using AI.Charts.Data;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.Charts.ChartElements;

/// <summary>
/// Filled surface plot: quads colored by Z value, drawn with painter's algorithm.
/// </summary>
[Serializable]
internal sealed class SurfacePlot3D : Base3DChart
{
    private readonly SurfaceData3D _data;

    /// <summary>Draw thin wireframe edges over filled quads.</summary>
    public bool ShowEdges { get; set; } = true;

    public SurfacePlot3D(string name, Vector xGrid, Vector yGrid, double[,] z) : base(name)
    {
        _data = new SurfaceData3D(xGrid, yGrid, z);
        BoundsXMin = xGrid.Min();
        BoundsXMax = xGrid.Max();
        BoundsYMin = yGrid.Min();
        BoundsYMax = yGrid.Max();
        BoundsZMin = _data.ZMin;
        BoundsZMax = _data.ZMax;
    }

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        Camera3D cam = vp.Camera3D;
        if (cam == null || _data.Rows < 2 || _data.Cols < 2) return;

        SKRect pr = vp.PlotRect;
        int rows = _data.Rows;
        int cols = _data.Cols;
        double zRange = BoundsZMax - BoundsZMin;
        if (zRange < 1e-30) zRange = 1;

        var faces = new List<(double depth, int i, int j)>((rows - 1) * (cols - 1));
        for (int i = 0; i < rows - 1; i++)
        for (int j = 0; j < cols - 1; j++)
        {
            double z00 = _data.Z[i, j], z10 = _data.Z[i + 1, j];
            double z01 = _data.Z[i, j + 1], z11 = _data.Z[i + 1, j + 1];
            if (double.IsNaN(z00) || double.IsNaN(z10) || double.IsNaN(z01) || double.IsNaN(z11))
                continue;
            double cx = (_data.XGrid[i] + _data.XGrid[i + 1]) * 0.5;
            double cy = (_data.YGrid[j] + _data.YGrid[j + 1]) * 0.5;
            double cz = (z00 + z10 + z01 + z11) * 0.25;
            cam.ProjectToScreen(cx, cy, cz, pr, out double depth);
            faces.Add((depth, i, j));
        }

        faces.Sort((a, b) => b.depth.CompareTo(a.depth));

        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var edgePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.4f,
            Color = new SKColor(0, 0, 0, 40)
        };

        foreach (var (_, i, j) in faces)
        {
            SKPoint p0 = cam.ProjectToScreen(_data.XGrid[i],     _data.YGrid[j],     _data.Z[i, j],         pr);
            SKPoint p1 = cam.ProjectToScreen(_data.XGrid[i + 1], _data.YGrid[j],     _data.Z[i + 1, j],     pr);
            SKPoint p2 = cam.ProjectToScreen(_data.XGrid[i + 1], _data.YGrid[j + 1], _data.Z[i + 1, j + 1], pr);
            SKPoint p3 = cam.ProjectToScreen(_data.XGrid[i],     _data.YGrid[j + 1], _data.Z[i, j + 1],     pr);

            double avgZ = (_data.Z[i, j] + _data.Z[i + 1, j] + _data.Z[i, j + 1] + _data.Z[i + 1, j + 1]) * 0.25;
            double t = (avgZ - BoundsZMin) / zRange;

            SKColor col = UseColormap
                ? Colormap.Map(t, ColormapKind)
                : ElementColor;
            fillPaint.Color = col;

            using var path = new SKPath();
            path.MoveTo(p0);
            path.LineTo(p1);
            path.LineTo(p2);
            path.LineTo(p3);
            path.Close();

            canvas.DrawPath(path, fillPaint);

            if (ShowEdges)
                canvas.DrawPath(path, edgePaint);
        }
    }
}
