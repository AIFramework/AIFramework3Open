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

    /// <summary>
    /// Half-extents of the bounding box per axis (set during auto-fit). Each axis is scaled to the same cube, as Plotly
    /// does by default: with one common extent an axis in small units (a learning rate of 0.01..1 next to a depth of
    /// 1..4) collapsed the surface into a sliver.
    /// </summary>
    internal double ExtentX { get; set; } = 1;
    internal double ExtentY { get; set; } = 1;
    internal double ExtentZ { get; set; } = 1;

    /// <summary>
    /// Project a 3D world point to 2D screen pixel coordinates within the given plot rectangle.
    /// Returns the screen point and the depth value (larger = farther from camera).
    /// </summary>
    public SKPoint ProjectToScreen(double wx, double wy, double wz, SKRect plotRect, out double depth)
    {
        double dx = (wx - CenterX) / ExtentX;
        double dy = (wy - CenterY) / ExtentY;
        double dz = (wz - CenterZ) / ExtentZ;

        double az = _azimuth * Math.PI / 180.0;
        double el = _elevation * Math.PI / 180.0;

        double cosA = Math.Cos(az), sinA = Math.Sin(az);
        double cosE = Math.Cos(el), sinE = Math.Sin(el);

        // Rotate around Z (azimuth)
        double rx = dx * cosA - dy * sinA;
        double ry = dx * sinA + dy * cosA;
        double rz = dz;

        // Tilt by elevation: the camera looks down at the scene from above, Z stays up on screen. Screen up is Z (and,
        // seen from above, the far side rises); depth grows away from the camera, so higher points are nearer to it.
        // Before, screen up was taken from the horizontal axis and Z went into depth: a surface was seen flat from above.
        double ex = rx;
        double ey = rz * cosE + ry * sinE;
        double ez = ry * cosE - rz * sinE;

        depth = ez;

        double ext = _distance;
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
        ExtentX = Half(xMin, xMax);
        ExtentY = Half(yMin, yMax);
        ExtentZ = Half(zMin, zMax);
    }

    /// <summary>Half of the range; a flat axis (all values equal) gets 1 so that the projection does not blow up.</summary>
    private static double Half(double min, double max)
    {
        double half = (max - min) * 0.5;
        return half > 1e-12 ? half : 1;
    }
}
