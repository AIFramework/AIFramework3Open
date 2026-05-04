using System;
using System.IO;
using System.Reflection;
using AI.Charts.Data;
using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.Charts.JS;

/// <summary>
/// High-level facade for converting <see cref="ChartView"/> instances to interactive
/// Plotly.js charts. Analogous to <c>AI.Charts.Avalonia.ChartViewControl</c> and
/// <c>AI.Charts.WinForms.ChartVisual</c>, but targets browser-based rendering via Plotly.js.
/// </summary>
public static class PlotlyChartRenderer
{
    /// <summary>
    /// Converts a <see cref="ChartView"/> into Plotly.js-compatible JSON.
    /// Returns <c>null</c> when the chart has a background image (e.g. decision boundary),
    /// is a bare 3D layout without explicit PlotlyBuilder traces, or has no series data.
    /// <para>
    /// For 3D charts, use <see cref="PlotlyBuilder"/> directly to add surface/scatter3d traces.
    /// </para>
    /// </summary>
    public static string? ToPlotlyJson(ChartView chartView)
    {
        if (chartView == null) throw new ArgumentNullException(nameof(chartView));

        ChartExport export = chartView.Export();

        if (export.LayoutKind == "graph" && export.Graph != null)
            return GraphToPlotlyJson(export);

        if (export.HasBackground || export.LayoutKind == "3d" || export.Series.Count == 0)
            return null;

        var pb = new PlotlyBuilder
        {
            Title = export.Title,
            AxisX = export.AxisX,
            AxisY = export.AxisY,
            IsLogY = export.IsLogScale,
        };

        foreach (ChartSeriesExport s in export.Series)
        {
            string hex = $"#{s.ColorR:X2}{s.ColorG:X2}{s.ColorB:X2}";
            switch (s.Type)
            {
                case "line":
                    pb.AddLine(s.X, s.Y, s.Name, hex, Math.Max(2, s.Width));
                    break;
                case "spline":
                    pb.AddLine(s.X, s.Y, s.Name, hex, Math.Max(2, s.Width), "spline");
                    break;
                case "bar":
                    pb.AddBar2D(s.X, s.Y, s.Name, hex);
                    break;
                case "scatter":
                    int nPts = s.X.Length;
                    int mSize = nPts >= 10000 ? 5 : nPts >= 5000 ? 7 : 10;
                    pb.AddScatter2D(s.X, s.Y, s.Name, hex, mSize);
                    break;
                case "area":
                    pb.AddArea(s.X, s.Y, s.Name, hex);
                    break;
                case "polar":
                    var thetaDeg = new double[s.X.Length];
                    for (int i = 0; i < s.X.Length; i++)
                        thetaDeg[i] = s.X[i] * 180.0 / Math.PI;
                    pb.AddPolarLine(thetaDeg, s.Y, s.Name, hex, Math.Max(1, s.Width));
                    break;
                case "pie":
                    pb.AddPie(s.X, s.Y, s.Name);
                    break;
            }
        }

        return pb.Build();
    }

    /// <summary>
    /// Applies a mathematical transform (FFT spectrum, derivative, integral, histogram)
    /// to all series in the given <see cref="ChartView"/> using the framework's math libraries,
    /// and returns the result as Plotly.js-compatible JSON for popup rendering.
    /// </summary>
    /// <param name="source">The original chart whose series will be transformed.</param>
    /// <param name="action">One of: <c>"fft"</c>, <c>"diff"</c>, <c>"integ"</c>, <c>"hist"</c>.</param>
    /// <returns>Plotly JSON string, or <c>null</c> if the chart has no transformable series.</returns>
    public static string? ComputeTransform(ChartView source, string action)
    {
        if (source == null) return null;

        ChartExport export = source.Export();
        if (export.Series.Count == 0) return null;

        var cv = new ChartView();

        string axisX = export.AxisX ?? "";
        string axisY = export.AxisY ?? "";

        switch (action)
        {
            case "fft":
                cv.ChartName = "Amplitude spectrum (Hamming window)";
                cv.LabelX = (axisX == "X-axis" || axisX.Contains("с") || axisX.Contains("s")) ? "Гц" : "1/" + (string.IsNullOrEmpty(axisX) ? "x" : axisX);
                cv.LabelY = "|Амплитуда|";
                foreach (var s in export.Series)
                {
                    if (s.X.Length < 4 || s.Y.Length < 4) continue;
                    var color = new SKColor(s.ColorR, s.ColorG, s.ColorB);
                    cv.AddSpectrum(new Vector(s.X), new Vector(s.Y), color, s.Name ?? "");
                }
                break;

            case "diff":
                cv.ChartName = export.Title ?? "";
                cv.LabelX = axisX;
                cv.LabelY = axisY.Contains("[Производная]") ? axisY : (string.IsNullOrEmpty(axisY) ? "y" : axisY) + " [Производная]";
                foreach (var s in export.Series)
                {
                    if (s.X.Length < 2 || s.Y.Length < 2) continue;
                    var color = new SKColor(s.ColorR, s.ColorG, s.ColorB);
                    cv.AddDiff(new Vector(s.X), new Vector(s.Y), color, s.Name ?? "", 2);
                }
                break;

            case "integ":
                cv.ChartName = export.Title ?? "";
                cv.LabelX = axisX;
                cv.LabelY = axisY.Contains("[Интеграл]") ? axisY : (string.IsNullOrEmpty(axisY) ? "y" : axisY) + " [Интеграл]";
                foreach (var s in export.Series)
                {
                    if (s.X.Length < 2 || s.Y.Length < 2) continue;
                    var color = new SKColor(s.ColorR, s.ColorG, s.ColorB);
                    cv.AddIntegr(new Vector(s.X), new Vector(s.Y), color, s.Name ?? "", 2);
                }
                break;

            case "hist":
                cv.ChartName = "Гистограмма";
                cv.LabelX = string.IsNullOrEmpty(axisY) || axisY == "Ось Y" ? "Значения функции" : axisY;
                cv.LabelY = "p(x)";
                foreach (var s in export.Series)
                {
                    if (s.Y.Length < 4) continue;
                    var color = new SKColor(s.ColorR, s.ColorG, s.ColorB);
                    cv.AddHistoramm(new Vector(s.Y), color, s.Name ?? "");
                }
                break;

            default:
                return null;
        }

        return ToPlotlyJson(cv);
    }

    /// <summary>
    /// Конвертирует данные графа в Plotly JSON через <see cref="PlotlyBuilder"/>.
    /// </summary>
    private static string? GraphToPlotlyJson(ChartExport export)
    {
        var graph = export.Graph;
        if (graph == null || graph.Nodes.Count == 0) return null;

        var nodes = new (double x, double y, string label, int group)[graph.Nodes.Count];
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var n = graph.Nodes[i];
            nodes[i] = (n.X, n.Y, n.Label, n.Group);
        }

        var edges = new (int from, int to)[graph.Edges.Count];
        for (int i = 0; i < graph.Edges.Count; i++)
            edges[i] = (graph.Edges[i].SourceIndex, graph.Edges[i].TargetIndex);

        var pb = new PlotlyBuilder { Title = export.Title };
        pb.AddDirectedGraph(nodes, edges);
        return pb.Build();
    }

    /// <summary>
    /// Returns the JavaScript interop script that provides the <c>window.renderPlotly</c>
    /// and <c>window.destroyPlotly</c> functions. Embed this into the page once.
    /// </summary>
    public static string GetInteropScript()
    {
        Assembly asm = typeof(PlotlyChartRenderer).Assembly;
        using Stream? stream = asm.GetManifestResourceStream("AI.Charts.JS.plotly-interop.js");
        if (stream == null)
            throw new InvalidOperationException("Embedded resource 'plotly-interop.js' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
