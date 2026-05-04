using System;
using SkiaSharp;

namespace AI.Charts.Rendering;

/// <summary>
/// Область построения и преобразования координат для SkiaSharp.
/// </summary>
internal sealed class ChartViewport
{
    public float Width { get; set; }
    public float Height { get; set; }
    public SKRect PlotRect { get; set; }
    public double XMin { get; set; }
    public double XMax { get; set; }
    public double YMin { get; set; }
    public double YMax { get; set; }
    public bool LogY { get; set; }
    public SKColor Foreground { get; set; }
    public SKColor Background { get; set; }
    public SKColor Grid { get; set; }

    public float PolarCx { get; set; }
    public float PolarCy { get; set; }
    /// <summary>Максимум данных по радиусу (масштаб).</summary>
    public double PolarDataRadiusMax { get; set; } = 1;
    /// <summary>Радиус окружности полярного графика в пикселях.</summary>
    public float PolarRingRadiusPx { get; set; }

    public float PieCx { get; set; }
    public float PieCy { get; set; }
    public float PieRadius { get; set; }

    /// <summary>Camera for 3D chart layouts.</summary>
    public Camera3D Camera3D { get; set; }

    /// <summary>Число интервалов сетки по X (подписи и линии совпадают).</summary>
    public int GridDivisionsX { get; set; } = 8;

    /// <summary>Число интервалов сетки по Y.</summary>
    public int GridDivisionsY { get; set; } = 8;

    public float XToPx(double x)
    {
        double dx = XMax - XMin;
        if (Math.Abs(dx) < 1e-30)
        {
            dx = 1;
        }

        double t = (x - XMin) / dx;
        return PlotRect.Left + (float)(t * PlotRect.Width);
    }

    public float YToPx(double y)
    {
        if (LogY)
        {
            double ly = Math.Log10(Math.Max(y, 1e-300));
            double lmin = Math.Log10(Math.Max(YMin, 1e-300));
            double lmax = Math.Log10(Math.Max(YMax, 1e-300));
            double d = lmax - lmin;
            if (Math.Abs(d) < 1e-30)
            {
                d = 1;
            }

            double t = (ly - lmin) / d;
            return PlotRect.Bottom - (float)(t * PlotRect.Height);
        }

        double dy = YMax - YMin;
        if (Math.Abs(dy) < 1e-30)
        {
            dy = 1;
        }

        double t2 = (y - YMin) / dy;
        return PlotRect.Bottom - (float)(t2 * PlotRect.Height);
    }

    public double PxToX(float px)
    {
        double dx = XMax - XMin;
        if (Math.Abs(dx) < 1e-30)
        {
            dx = 1;
        }

        double t = (px - PlotRect.Left) / PlotRect.Width;
        return XMin + t * dx;
    }

    public double PxToY(float py)
    {
        double t = (PlotRect.Bottom - py) / PlotRect.Height;
        if (LogY)
        {
            double lmin = Math.Log10(Math.Max(YMin, 1e-300));
            double lmax = Math.Log10(Math.Max(YMax, 1e-300));
            double d = lmax - lmin;
            if (Math.Abs(d) < 1e-30)
            {
                d = 1;
            }

            double ly = lmin + t * d;
            return Math.Pow(10, ly);
        }

        double dy = YMax - YMin;
        if (Math.Abs(dy) < 1e-30)
        {
            dy = 1;
        }

        return YMin + t * dy;
    }

    /// <summary>
    /// Точка в полярных координатах: угол (рад), радиус (значение по Y).
    /// </summary>
    public SKPoint PolarToPx(double angle, double radius)
    {
        double rMax = PolarDataRadiusMax > 1e-30 ? PolarDataRadiusMax : 1;
        double t = radius / rMax;
        if (t < 0)
        {
            t = 0;
        }

        if (t > 1)
        {
            t = 1;
        }

        float rr = PolarRingRadiusPx * (float)t;
        float x = PolarCx + rr * (float)Math.Cos(angle);
        float y = PolarCy - rr * (float)Math.Sin(angle);
        return new SKPoint(x, y);
    }
}
