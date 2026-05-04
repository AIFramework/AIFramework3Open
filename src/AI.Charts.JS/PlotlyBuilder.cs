using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AI.Charts.JS;

/// <summary>
/// Low-level builder that constructs Plotly.js-compatible JSON specs
/// for both 2D (scatter, bar, area, polar, pie) and 3D (surface, scatter3d) traces.
/// </summary>
public sealed class PlotlyBuilder
{
    private readonly List<object> _traces = new();
    private readonly List<object> _shapes = new();
    private readonly List<object> _annotations = new();

    public string? Title { get; set; }
    public string? AxisX { get; set; }
    public string? AxisY { get; set; }
    public string? AxisZ { get; set; }
    public bool Is3D { get; set; }
    public bool IsLogY { get; set; }
    public bool IsPolar { get; set; }
    public bool IsGraph { get; set; }
    public double CameraEyeX { get; set; } = 1.5;
    public double CameraEyeY { get; set; } = 1.5;
    public double CameraEyeZ { get; set; } = 1.2;

    #region 2D traces

    public void AddLine(double[] x, double[] y, string? name = null,
        string? color = null, int width = 2, string shape = "linear")
    {
        var trace = new Dictionary<string, object>
        {
            ["type"] = "scatter",
            ["mode"] = "lines",
            ["x"] = x,
            ["y"] = y,
        };
        var line = new Dictionary<string, object> { ["width"] = width };
        if (shape == "spline") line["shape"] = "spline";
        if (!string.IsNullOrEmpty(color)) line["color"] = color;
        trace["line"] = line;
        if (!string.IsNullOrEmpty(name)) trace["name"] = name;
        _traces.Add(trace);
    }

    public void AddScatter2D(double[] x, double[] y, string? name = null,
        string? color = null, int markerSize = 6)
    {
        var trace = new Dictionary<string, object>
        {
            ["type"] = "scatter",
            ["mode"] = "markers",
            ["x"] = x,
            ["y"] = y,
        };
        var marker = new Dictionary<string, object> { ["size"] = markerSize };
        if (!string.IsNullOrEmpty(color)) marker["color"] = color;
        trace["marker"] = marker;
        if (!string.IsNullOrEmpty(name)) trace["name"] = name;
        _traces.Add(trace);
    }

    public void AddBar2D(double[] x, double[] y, string? name = null,
        string? color = null, double opacity = 1.0)
    {
        var trace = new Dictionary<string, object>
        {
            ["type"] = "bar",
            ["x"] = x,
            ["y"] = y,
            ["opacity"] = opacity,
        };
        if (!string.IsNullOrEmpty(color))
            trace["marker"] = new Dictionary<string, object> { ["color"] = color };
        if (!string.IsNullOrEmpty(name)) trace["name"] = name;
        _traces.Add(trace);
    }

    public void AddArea(double[] x, double[] y, string? name = null,
        string? color = null)
    {
        var trace = new Dictionary<string, object>
        {
            ["type"] = "scatter",
            ["mode"] = "lines",
            ["x"] = x,
            ["y"] = y,
            ["fill"] = "tozeroy",
        };
        var line = new Dictionary<string, object> { ["width"] = 2 };
        if (!string.IsNullOrEmpty(color))
        {
            line["color"] = color;
            trace["fillcolor"] = color.Length == 7 ? color + "64" : color;
        }
        trace["line"] = line;
        if (!string.IsNullOrEmpty(name)) trace["name"] = name;
        _traces.Add(trace);
    }

    public void AddPolarLine(double[] theta, double[] r, string? name = null,
        string? color = null, int width = 2)
    {
        IsPolar = true;
        var trace = new Dictionary<string, object>
        {
            ["type"] = "scatterpolar",
            ["mode"] = "lines",
            ["theta"] = theta,
            ["r"] = r,
        };
        var line = new Dictionary<string, object> { ["width"] = width };
        if (!string.IsNullOrEmpty(color)) line["color"] = color;
        trace["line"] = line;
        if (!string.IsNullOrEmpty(name)) trace["name"] = name;
        _traces.Add(trace);
    }

    public void AddPie(double[] labels, double[] values, string? name = null)
    {
        var trace = new Dictionary<string, object>
        {
            ["type"] = "pie",
            ["labels"] = labels,
            ["values"] = values,
        };
        if (!string.IsNullOrEmpty(name)) trace["name"] = name;
        _traces.Add(trace);
    }

    #endregion 2D traces

    #region Heatmap

    public void AddHeatmap(double[] x, double[] y, double[][] z,
        string? colorscale = null, double opacity = 0.85,
        bool showScale = false, double? zMin = null, double? zMax = null)
    {
        var trace = new Dictionary<string, object>
        {
            ["type"] = "heatmap",
            ["x"] = x,
            ["y"] = y,
            ["z"] = z,
            ["colorscale"] = colorscale ?? "Viridis",
            ["opacity"] = opacity,
            ["showscale"] = showScale,
            ["hoverinfo"] = "x+y+z",
        };
        if (zMin.HasValue) trace["zmin"] = zMin.Value;
        if (zMax.HasValue) trace["zmax"] = zMax.Value;
        _traces.Add(trace);
    }

    public void AddHeatmapDiscrete(double[] x, double[] y, int[][] classIds,
        string[]? colorMap = null, double opacity = 0.4)
    {
        var nClasses = 0;
        foreach (var row in classIds)
            foreach (var v in row)
                if (v + 1 > nClasses) nClasses = v + 1;

        var defaultColors = new[] {
            "rgb(55,126,184)", "rgb(228,26,28)", "rgb(77,175,74)",
            "rgb(152,78,163)", "rgb(255,127,0)", "rgb(166,86,40)",
            "rgb(247,129,191)", "rgb(153,153,153)", "rgb(0,190,190)",
            "rgb(200,200,50)"
        };
        var palette = colorMap ?? defaultColors;

        var scale = new List<object[]>();
        for (int i = 0; i < nClasses; i++)
        {
            double lo = (double)i / nClasses;
            double hi = (double)(i + 1) / nClasses;
            var c = palette[i % palette.Length];
            scale.Add(new object[] { lo, c });
            scale.Add(new object[] { hi, c });
        }

        var zNorm = new double[classIds.Length][];
        for (int i = 0; i < classIds.Length; i++)
        {
            zNorm[i] = new double[classIds[i].Length];
            for (int j = 0; j < classIds[i].Length; j++)
                zNorm[i][j] = nClasses > 1
                    ? ((double)classIds[i][j] + 0.5) / nClasses
                    : 0.5;
        }

        var trace = new Dictionary<string, object>
        {
            ["type"] = "heatmap",
            ["x"] = x,
            ["y"] = y,
            ["z"] = zNorm,
            ["colorscale"] = scale.ToArray(),
            ["opacity"] = opacity,
            ["showscale"] = false,
            ["hoverinfo"] = "skip",
        };
        _traces.Add(trace);
    }

    #endregion Heatmap

    #region 3D traces

    public void AddSurface(double[] xGrid, double[] yGrid, double[,] z,
        string? name = null, string colorscale = "Viridis", double opacity = 1.0,
        bool showEdges = true)
    {
        Is3D = true;
        int rows = z.GetLength(0), cols = z.GetLength(1);
        var zArr = new double[rows][];
        var surfMask = new int[rows][];
        bool hasNan = false;
        for (int i = 0; i < rows; i++)
        {
            zArr[i] = new double[cols];
            surfMask[i] = new int[cols];
            for (int j = 0; j < cols; j++)
            {
                bool nan = double.IsNaN(z[i, j]);
                zArr[i][j] = nan ? 0 : z[i, j];
                surfMask[i][j] = nan ? 0 : 1;
                if (nan) hasNan = true;
            }
        }

        var trace = new Dictionary<string, object>
        {
            ["type"] = "surface", ["x"] = yGrid, ["y"] = xGrid, ["z"] = zArr,
            ["colorscale"] = colorscale, ["opacity"] = opacity,
            ["showscale"] = _traces.Count == 0,
        };
        if (hasNan) trace["surfacecolor"] = surfMask;
        if (!string.IsNullOrEmpty(name)) trace["name"] = name;
        if (showEdges)
            trace["contours"] = new Dictionary<string, object>
            {
                ["x"] = new Dictionary<string, object> { ["highlight"] = false },
                ["y"] = new Dictionary<string, object> { ["highlight"] = false },
                ["z"] = new Dictionary<string, object> { ["highlight"] = false, ["show"] = true, ["color"] = "#444", ["width"] = 1 }
            };
        _traces.Add(trace);
    }

    public void AddScatter3D(double[] x, double[] y, double[] z,
        string? name = null, string? color = null, int markerSize = 3,
        string colorscale = "Viridis", bool colorByZ = false)
    {
        Is3D = true;
        var trace = new Dictionary<string, object>
        {
            ["type"] = "scatter3d", ["mode"] = "markers",
            ["x"] = x, ["y"] = y, ["z"] = z,
        };
        var marker = new Dictionary<string, object> { ["size"] = markerSize, ["opacity"] = 0.85 };
        if (colorByZ) { marker["color"] = z; marker["colorscale"] = colorscale; marker["showscale"] = false; }
        else if (!string.IsNullOrEmpty(color)) marker["color"] = color;
        trace["marker"] = marker;
        if (!string.IsNullOrEmpty(name)) trace["name"] = name;
        _traces.Add(trace);
    }

    #endregion 3D traces

    #region Graph / Tree

    private const double NodeHalfW = 0.55;
    private const double NodeHalfH = 0.16;

    private static readonly string[] DefaultGraphPalette =
    [
        "#818cf8", "#34d399", "#fbbf24", "#f87171",
        "#60a5fa", "#c084fc", "#2dd4bf", "#fb923c"
    ];

    /// <summary>
    /// Добавляет визуализацию направленного графа через Plotly shapes (прямоугольники),
    /// annotations (подписи) и arrow-annotations (стрелки зависимостей).
    /// </summary>
    public void AddDirectedGraph(
        (double x, double y, string label, int group)[] nodes,
        (int from, int to)[] edges,
        string[]? groupColors = null)
    {
        IsGraph = true;
        var palette = groupColors ?? DefaultGraphPalette;

        double xMin = double.MaxValue, xMax = double.MinValue;
        var tierYs = new Dictionary<int, double>();
        foreach (var n in nodes)
        {
            if (n.x - NodeHalfW < xMin) xMin = n.x - NodeHalfW;
            if (n.x + NodeHalfW > xMax) xMax = n.x + NodeHalfW;
            tierYs.TryAdd(n.group, n.y);
        }

        foreach (var kv in tierYs)
        {
            _shapes.Add(new Dictionary<string, object>
            {
                ["type"] = "rect",
                ["xref"] = "paper", ["yref"] = "y",
                ["x0"] = 0, ["x1"] = 1,
                ["y0"] = kv.Value - NodeHalfH - 0.08,
                ["y1"] = kv.Value + NodeHalfH + 0.08,
                ["fillcolor"] = kv.Key % 2 == 0 ? "rgba(255,255,255,0.02)" : "rgba(255,255,255,0.04)",
                ["line"] = new Dictionary<string, object> { ["width"] = 0 },
                ["layer"] = "below",
            });

            _annotations.Add(new Dictionary<string, object>
            {
                ["x"] = 0.01, ["y"] = kv.Value,
                ["xref"] = "paper", ["yref"] = "y",
                ["text"] = $"<b>T{kv.Key}</b>",
                ["showarrow"] = false,
                ["font"] = new Dictionary<string, object>
                {
                    ["size"] = 10, ["color"] = "#475569"
                },
                ["xanchor"] = "left", ["yanchor"] = "middle",
            });
        }

        var hoverX = new List<double>();
        var hoverY = new List<double>();
        var hoverText = new List<string>();

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            var color = palette[Math.Abs(n.group) % palette.Length];

            _shapes.Add(new Dictionary<string, object>
            {
                ["type"] = "rect",
                ["xref"] = "x", ["yref"] = "y",
                ["x0"] = n.x - NodeHalfW, ["y0"] = n.y - NodeHalfH,
                ["x1"] = n.x + NodeHalfW, ["y1"] = n.y + NodeHalfH,
                ["fillcolor"] = color + "20",
                ["line"] = new Dictionary<string, object>
                {
                    ["color"] = color, ["width"] = 2
                },
                ["layer"] = "above",
            });

            var displayLabel = n.label.Length > 24 ? n.label[..23] + "…" : n.label;

            _annotations.Add(new Dictionary<string, object>
            {
                ["x"] = n.x, ["y"] = n.y,
                ["xref"] = "x", ["yref"] = "y",
                ["text"] = $"<b>{displayLabel}</b>",
                ["showarrow"] = false,
                ["font"] = new Dictionary<string, object>
                {
                    ["size"] = 11, ["color"] = "#e2e8f0", ["family"] = "Inter, system-ui, sans-serif"
                },
                ["xanchor"] = "center", ["yanchor"] = "middle",
            });

            hoverX.Add(n.x);
            hoverY.Add(n.y);
            hoverText.Add($"<b>{n.label}</b><br>Ярус {n.group}");
        }

        _traces.Add(new Dictionary<string, object>
        {
            ["type"] = "scatter",
            ["mode"] = "markers",
            ["x"] = hoverX.ToArray(),
            ["y"] = hoverY.ToArray(),
            ["text"] = hoverText.ToArray(),
            ["hoverinfo"] = "text",
            ["hoverlabel"] = new Dictionary<string, object>
            {
                ["bgcolor"] = "#1e293b",
                ["bordercolor"] = "#6366f1",
                ["font"] = new Dictionary<string, object> { ["size"] = 12, ["color"] = "#f1f5f9" }
            },
            ["showlegend"] = false,
            ["marker"] = new Dictionary<string, object>
            {
                ["size"] = 50, ["opacity"] = 0
            }
        });

        foreach (var (from, to) in edges)
        {
            if (from < 0 || from >= nodes.Length || to < 0 || to >= nodes.Length) continue;
            var src = nodes[from];
            var tgt = nodes[to];

            _annotations.Add(new Dictionary<string, object>
            {
                ["x"] = tgt.x, ["y"] = tgt.y + NodeHalfH,
                ["ax"] = src.x, ["ay"] = src.y - NodeHalfH,
                ["xref"] = "x", ["yref"] = "y",
                ["axref"] = "x", ["ayref"] = "y",
                ["showarrow"] = true,
                ["arrowhead"] = 3,
                ["arrowsize"] = 1.3,
                ["arrowwidth"] = 1.5,
                ["arrowcolor"] = "#6366f1",
                ["opacity"] = 0.5,
                ["standoff"] = 2,
                ["startstandoff"] = 2,
                ["text"] = "",
            });
        }
    }

    #endregion Graph / Tree

    #region Build

    public string Build()
    {
        var spec = new Dictionary<string, object>
        {
            ["traces"] = _traces,
            ["is3d"] = Is3D,
            ["isPolar"] = IsPolar,
            ["isLogY"] = IsLogY,
            ["isGraph"] = IsGraph,
        };
        if (_shapes.Count > 0) spec["shapes"] = _shapes;
        if (_annotations.Count > 0) spec["annotations"] = _annotations;
        if (!string.IsNullOrEmpty(Title)) spec["title"] = Title;
        if (!string.IsNullOrEmpty(AxisX)) spec["axisX"] = AxisX;
        if (!string.IsNullOrEmpty(AxisY)) spec["axisY"] = AxisY;
        if (!string.IsNullOrEmpty(AxisZ)) spec["axisZ"] = AxisZ;
        if (Is3D)
            spec["camera"] = new Dictionary<string, object>
            {
                ["eye"] = new Dictionary<string, object>
                    { ["x"] = CameraEyeX, ["y"] = CameraEyeY, ["z"] = CameraEyeZ }
            };

        return JsonSerializer.Serialize(spec, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    /// <summary>Maps AI.Charts ColormapKind name to Plotly colorscale.</summary>
    public static string MapColorscale(string colormapKind) => colormapKind switch
    {
        "Viridis" => "Viridis",
        "Thermal" => "Hot",
        "Grayscale" => "Greys",
        _ => "Jet"
    };
    #endregion Build

}