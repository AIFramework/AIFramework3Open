using AI.Charts.Data;
using System.Collections.Generic;

namespace AI.Charts;

/// <summary>
/// Exported chart data for external consumers (e.g. Plotly.js rendering).
/// </summary>
public sealed class ChartExport
{
    public string Title { get; }
    public string AxisX { get; }
    public string AxisY { get; }
    public bool IsLogScale { get; }
    public bool HasBackground { get; }
    /// <summary>"cartesian", "polar", "pie", "3d", or "graph".</summary>
    public string LayoutKind { get; }
    public IReadOnlyList<ChartSeriesExport> Series { get; }

    /// <summary>Данные графа (если LayoutKind == "graph").</summary>
    public GraphData Graph { get; }

    public ChartExport(string title, string axisX, string axisY,
        bool isLogScale, bool hasBackground, string layoutKind,
        IReadOnlyList<ChartSeriesExport> series, GraphData graph = null)
    {
        Title = title;
        AxisX = axisX;
        AxisY = axisY;
        IsLogScale = isLogScale;
        HasBackground = hasBackground;
        LayoutKind = layoutKind;
        Series = series;
        Graph = graph;
    }
}

/// <summary>
/// A single chart series exported for external rendering.
/// </summary>
public sealed class ChartSeriesExport
{
    public string Name { get; }
    /// <summary>"line", "spline", "bar", "scatter", "area", "polar", "pie".</summary>
    public string Type { get; }
    public double[] X { get; }
    public double[] Y { get; }
    public byte ColorR { get; }
    public byte ColorG { get; }
    public byte ColorB { get; }
    public byte ColorA { get; }
    public int Width { get; }

    public ChartSeriesExport(string name, string type, double[] x, double[] y,
        byte colorR, byte colorG, byte colorB, byte colorA, int width)
    {
        Name = name;
        Type = type;
        X = x;
        Y = y;
        ColorR = colorR;
        ColorG = colorG;
        ColorB = colorB;
        ColorA = colorA;
        Width = width;
    }
}
