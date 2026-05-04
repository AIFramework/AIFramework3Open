using AI.Charts.Data;
using AI.Charts.Rendering;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace AI.Charts.ChartElements;

/// <summary>
/// Визуализация направленного графа: узлы (скруглённые прямоугольники) и стрелки зависимостей.
/// Используется для отображения DAG планов, деревьев задач, конечных автоматов.
/// </summary>
[Serializable]
internal class GraphChart : BaseChart
{
    private readonly GraphData _graph;

    internal GraphData Graph => _graph;

    private const float NodeW = 140f;
    private const float NodeH = 40f;
    private const float CornerR = 6f;
    private const float ArrowSize = 6f;

    private static readonly SKColor[] GroupPalette =
    [
        new(99, 102, 241),   // indigo
        new(34, 197, 94),    // green
        new(245, 158, 11),   // amber
        new(239, 68, 68),    // red
        new(59, 130, 246),   // blue
        new(168, 85, 247),   // purple
        new(20, 184, 166),   // teal
        new(249, 115, 22),   // orange
    ];

    private double _xMin, _xMax, _yMin, _yMax;

    public GraphChart(string name, GraphData graph) : base(name)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        ComputeBounds();
    }

    private void ComputeBounds()
    {
        if (_graph.Nodes.Count == 0) return;

        _xMin = _xMax = _graph.Nodes[0].X;
        _yMin = _yMax = _graph.Nodes[0].Y;

        foreach (var n in _graph.Nodes)
        {
            if (n.X < _xMin) _xMin = n.X;
            if (n.X > _xMax) _xMax = n.X;
            if (n.Y < _yMin) _yMin = n.Y;
            if (n.Y > _yMax) _yMax = n.Y;
        }

        double padX = (_xMax - _xMin) * 0.15 + 1.0;
        double padY = (_yMax - _yMin) * 0.15 + 0.5;
        _xMin -= padX;
        _xMax += padX;
        _yMin -= padY;
        _yMax += padY;

        var dummyX = new AI.DataStructs.Algebraic.Vector(new[] { _xMin, _xMax });
        var dummyY = new AI.DataStructs.Algebraic.Vector(new[] { _yMin, _yMax });
        data = new VectorBasedData();
        data.LoadData(dummyX, dummyY);
    }

    public override void Recalc(double min, double max)
    {
    }

    public override void Draw(SKCanvas canvas, ChartViewport vp)
    {
        if (_graph.Nodes.Count == 0) return;

        using var edgePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(99, 102, 241, 140),
            IsAntialias = true
        };

        // Рёбра
        foreach (var edge in _graph.Edges)
        {
            if (edge.SourceIndex < 0 || edge.SourceIndex >= _graph.Nodes.Count) continue;
            if (edge.TargetIndex < 0 || edge.TargetIndex >= _graph.Nodes.Count) continue;

            var src = _graph.Nodes[edge.SourceIndex];
            var tgt = _graph.Nodes[edge.TargetIndex];

            float x1 = vp.XToPx(src.X);
            float y1 = vp.YToPx(src.Y) + NodeH / 2;
            float x2 = vp.XToPx(tgt.X);
            float y2 = vp.YToPx(tgt.Y) - NodeH / 2;

            canvas.DrawLine(x1, y1, x2, y2, edgePaint);
            DrawArrowhead(canvas, x1, y1, x2, y2, edgePaint);
        }

        bool isDark = vp.Background.Red < 80;
        var textColor = isDark ? new SKColor(226, 232, 240) : new SKColor(30, 41, 59);
        var subColor = isDark ? new SKColor(148, 163, 184) : new SKColor(100, 116, 139);

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 11,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            Color = textColor
        };

        using var subPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 9,
            TextAlign = SKTextAlign.Center,
            Color = subColor
        };

        using var tierPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 10,
            TextAlign = SKTextAlign.Left,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            Color = isDark ? new SKColor(71, 85, 105) : new SKColor(148, 163, 184)
        };

        var drawnTiers = new HashSet<int>();
        foreach (var node in _graph.Nodes)
        {
            if (drawnTiers.Add(node.Group))
            {
                float ty = vp.YToPx(node.Y);
                canvas.DrawText($"T{node.Group}", vp.PlotRect.Left + 6, ty + 4, tierPaint);
            }
        }

        foreach (var node in _graph.Nodes)
        {
            float cx = vp.XToPx(node.X);
            float cy = vp.YToPx(node.Y);
            var rect = new SKRect(cx - NodeW / 2, cy - NodeH / 2, cx + NodeW / 2, cy + NodeH / 2);

            var groupColor = GroupPalette[Math.Abs(node.Group) % GroupPalette.Length];

            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = groupColor.WithAlpha(32),
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, CornerR, CornerR, fillPaint);

            using var borderPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                Color = groupColor,
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, CornerR, CornerR, borderPaint);

            var label = Truncate(node.Label, 22);
            canvas.DrawText(label, cx, cy + (string.IsNullOrEmpty(node.Subtitle) ? 4f : -2f), textPaint);

            if (!string.IsNullOrEmpty(node.Subtitle))
                canvas.DrawText(Truncate(node.Subtitle, 22), cx, cy + 12f, subPaint);
        }
    }

    private static void DrawArrowhead(SKCanvas canvas, float x1, float y1, float x2, float y2, SKPaint paint)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-3) return;

        dx /= len; dy /= len;
        float ax = (float)(x2 - dx * ArrowSize);
        float ay = (float)(y2 - dy * ArrowSize);
        float px = (float)(-dy * ArrowSize * 0.5);
        float py = (float)(dx * ArrowSize * 0.5);

        using var fill = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = paint.Color,
            IsAntialias = true
        };

        var path = new SKPath();
        path.MoveTo(x2, y2);
        path.LineTo(ax + px, ay + py);
        path.LineTo(ax - px, ay - py);
        path.Close();
        canvas.DrawPath(path, fill);
    }

    private static string Truncate(string text, int max)
        => text != null && text.Length > max ? text[..(max - 1)] + "…" : text ?? "";
}
