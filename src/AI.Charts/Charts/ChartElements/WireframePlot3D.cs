using System;
using AI.Charts.Data;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.Charts.ChartElements;

/// <summary>
/// Wireframe 3D plot: grid lines only (no filled faces).
/// </summary>
[Serializable]
internal sealed class WireframePlot3D : Base3DChart
{
    private readonly SurfaceData3D _data;

    public WireframePlot3D(string name, Vector xGrid, Vector yGrid, double[,] z) : base(name)
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
        double zRange = BoundsZMax - BoundsZMin;
        if (zRange < 1e-30) zRange = 1;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = BorderWidth
        };

        // Row lines (along Y for each X)
        for (int i = 0; i < _data.Rows; i++)
        {
            for (int j = 0; j < _data.Cols - 1; j++)
            {
                double z1 = _data.Z[i, j], z2 = _data.Z[i, j + 1];
                SKPoint a = cam.ProjectToScreen(_data.XGrid[i], _data.YGrid[j],     z1, pr);
                SKPoint b = cam.ProjectToScreen(_data.XGrid[i], _data.YGrid[j + 1], z2, pr);

                if (UseColormap)
                {
                    double t = ((z1 + z2) * 0.5 - BoundsZMin) / zRange;
                    paint.Color = Colormap.Map(t, ColormapKind);
                }
                else
                {
                    paint.Color = ElementColor;
                }

                canvas.DrawLine(a, b, paint);
            }
        }

        // Column lines (along X for each Y)
        for (int j = 0; j < _data.Cols; j++)
        {
            for (int i = 0; i < _data.Rows - 1; i++)
            {
                double z1 = _data.Z[i, j], z2 = _data.Z[i + 1, j];
                SKPoint a = cam.ProjectToScreen(_data.XGrid[i],     _data.YGrid[j], z1, pr);
                SKPoint b = cam.ProjectToScreen(_data.XGrid[i + 1], _data.YGrid[j], z2, pr);

                if (UseColormap)
                {
                    double t = ((z1 + z2) * 0.5 - BoundsZMin) / zRange;
                    paint.Color = Colormap.Map(t, ColormapKind);
                }
                else
                {
                    paint.Color = ElementColor;
                }

                canvas.DrawLine(a, b, paint);
            }
        }
    }
}
