using System;
using SkiaSharp;

namespace AI.Charts.Rendering;

/// <summary>
/// Camera for 3D chart rendering.
/// Orthographic projection: rotate around data center, then project onto XY screen plane.
/// </summary>
[Serializable]
public sealed class Camera3D
{
    private double _azimuth = 45;
    private double _elevation = 30;
    private double _distance = 2.5;

    /// <summary>Horizontal rotation around Z-up axis (degrees, 0..360).</summary>
    public double Azimuth
    {
        get => _azimuth;
        set => _azimuth = ((value % 360) + 360) % 360;
    }

    /// <summary>Vertical tilt (degrees, clamped to -89..89).</summary>
    public double Elevation
    {
        get => _elevation;
        set => _elevation = Math.Max(-89, Math.Min(89, value));
    }

    /// <summary>Scale factor (larger = farther / smaller chart).</summary>
    public double Distance
    {
        get => _distance;
        set => _distance = Math.Max(0.1, value);
    }

    /// <summary>Center of rotation in world coordinates.</summary>
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }

    /// <summary>Half-extent of the bounding box (set during auto-fit).</summary>
    internal double Extent { get; set; } = 1;

    /// <summary>
    /// Project a 3D world point to 2D screen pixel coordinates within the given plot rectangle.
    /// Returns the screen point and the depth value (larger = farther from camera).
    /// </summary>
    public SKPoint ProjectToScreen(double wx, double wy, double wz, SKRect plotRect, out double depth)
    {
        double dx = wx - CenterX;
        double dy = wy - CenterY;
        double dz = wz - CenterZ;

        double az = _azimuth * Math.PI / 180.0;
        double el = _elevation * Math.PI / 180.0;

        double cosA = Math.Cos(az), sinA = Math.Sin(az);
        double cosE = Math.Cos(el), sinE = Math.Sin(el);

        // Rotate around Z (azimuth)
        double rx = dx * cosA - dy * sinA;
        double ry = dx * sinA + dy * cosA;
        double rz = dz;

        // Rotate around X (elevation)
        double ex = rx;
        double ey = ry * cosE - rz * sinE;
        double ez = ry * sinE + rz * cosE;

        depth = ez;

        double ext = Math.Max(Extent, 1e-12) * _distance;
        double sx = ex / ext;
        double sy = ey / ext;

        float midX = plotRect.MidX;
        float midY = plotRect.MidY;
        float halfW = plotRect.Width * 0.38f;
        float halfH = plotRect.Height * 0.38f;
        float scale = Math.Min(halfW, halfH);

        float px = midX + (float)(sx * scale);
        float py = midY - (float)(sy * scale);

        return new SKPoint(px, py);
    }

    /// <summary>Shorthand without depth output.</summary>
    public SKPoint ProjectToScreen(double wx, double wy, double wz, SKRect plotRect)
    {
        return ProjectToScreen(wx, wy, wz, plotRect, out _);
    }

    /// <summary>
    /// Configure camera center and extent from the bounding box of the data.
    /// </summary>
    public void FitToBounds(double xMin, double xMax, double yMin, double yMax, double zMin, double zMax)
    {
        CenterX = (xMin + xMax) * 0.5;
        CenterY = (yMin + yMax) * 0.5;
        CenterZ = (zMin + zMax) * 0.5;
        double ex = (xMax - xMin) * 0.5;
        double ey = (yMax - yMin) * 0.5;
        double ez = (zMax - zMin) * 0.5;
        Extent = Math.Max(Math.Max(ex, ey), Math.Max(ez, 1e-12));
    }
}
