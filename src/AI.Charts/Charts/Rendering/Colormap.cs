using System;
using SkiaSharp;

namespace AI.Charts.Rendering;

/// <summary>Available colormap presets.</summary>
public enum ColormapKind
{
    Jet,
    Viridis,
    Thermal,
    Grayscale
}

/// <summary>
/// Maps a normalized value [0..1] to an SKColor using a predefined gradient.
/// </summary>
internal static class Colormap
{
    public static SKColor Map(double t, ColormapKind kind)
    {
        t = Math.Max(0, Math.Min(1, t));
        switch (kind)
        {
            case ColormapKind.Jet:      return Jet(t);
            case ColormapKind.Viridis:  return Viridis(t);
            case ColormapKind.Thermal:  return Thermal(t);
            case ColormapKind.Grayscale:
                byte g = (byte)(t * 255);
                return new SKColor(g, g, g);
            default:                    return Jet(t);
        }
    }

    private static SKColor Jet(double t)
    {
        // Classic Jet: blue -> cyan -> green -> yellow -> red
        byte r, g, b;
        if (t < 0.125)
        {
            r = 0; g = 0; b = (byte)(128 + t / 0.125 * 127);
        }
        else if (t < 0.375)
        {
            double s = (t - 0.125) / 0.25;
            r = 0; g = (byte)(s * 255); b = 255;
        }
        else if (t < 0.625)
        {
            double s = (t - 0.375) / 0.25;
            r = (byte)(s * 255); g = 255; b = (byte)(255 - s * 255);
        }
        else if (t < 0.875)
        {
            double s = (t - 0.625) / 0.25;
            r = 255; g = (byte)(255 - s * 255); b = 0;
        }
        else
        {
            double s = (t - 0.875) / 0.125;
            r = (byte)(255 - s * 127); g = 0; b = 0;
        }
        return new SKColor(r, g, b);
    }

    private static SKColor Viridis(double t)
    {
        // Simplified 5-stop Viridis
        (byte R, byte G, byte B)[] stops =
        {
            (68,  1,   84),
            (59,  82,  139),
            (33,  145, 140),
            (94,  201, 98),
            (253, 231, 37)
        };
        return LerpStops(t, stops);
    }

    private static SKColor Thermal(double t)
    {
        (byte R, byte G, byte B)[] stops =
        {
            (4,   35,  51),
            (25,  100, 120),
            (70,  160, 100),
            (220, 200, 60),
            (255, 100, 30),
            (200, 30,  30)
        };
        return LerpStops(t, stops);
    }

    private static SKColor LerpStops(double t, (byte R, byte G, byte B)[] stops)
    {
        int n = stops.Length - 1;
        double pos = t * n;
        int i = Math.Min((int)pos, n - 1);
        double frac = pos - i;
        byte r = (byte)(stops[i].R + frac * (stops[i + 1].R - stops[i].R));
        byte g = (byte)(stops[i].G + frac * (stops[i + 1].G - stops[i].G));
        byte b = (byte)(stops[i].B + frac * (stops[i + 1].B - stops[i].B));
        return new SKColor(r, g, b);
    }
}
