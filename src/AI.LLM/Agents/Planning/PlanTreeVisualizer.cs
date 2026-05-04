using System.Text;

namespace AI.LLM.Agents.Planning;

/// <summary>
/// Визуализация <see cref="PlanTree"/> в различных форматах:
/// SVG (inline для HTML/Blazor), Mermaid-диаграмма, текстовое дерево.
/// </summary>
public static class PlanTreeVisualizer
{
    private const int NodeWidth = 220;
    private const int NodeHeight = 52;
    private const int TierGap = 80;
    private const int NodeGap = 24;
    private const int PaddingX = 40;
    private const int PaddingY = 40;

    #region SVG

    /// <summary>
    /// Генерирует inline SVG с деревом ярусов: узлы, подписи, стрелки зависимостей.
    /// </summary>
    public static string ToSvg(PlanTree plan)
    {
        if (plan == null || plan.Steps.Count == 0)
            return "<svg></svg>";

        var positions = CalculateLayout(plan);
        var (svgW, svgH) = CalculateCanvasSize(positions);

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {svgW} {svgH}\" " +
                       $"width=\"100%\" style=\"max-width:{svgW}px;font-family:Inter,system-ui,sans-serif;\">");

        sb.AppendLine("<defs>");
        sb.AppendLine("  <marker id=\"arrowhead\" markerWidth=\"8\" markerHeight=\"6\" refX=\"8\" refY=\"3\" orient=\"auto\">");
        sb.AppendLine("    <polygon points=\"0 0, 8 3, 0 6\" fill=\"#6366f1\"/>");
        sb.AppendLine("  </marker>");
        sb.AppendLine("</defs>");

        // Стрелки зависимостей
        foreach (var step in plan.Steps)
        {
            if (!positions.TryGetValue(step.Id, out var to)) continue;
            foreach (var dep in step.DependsOn)
            {
                if (!positions.TryGetValue(dep, out var from)) continue;
                var (x1, y1) = (from.CenterX, from.Bottom);
                var (x2, y2) = (to.CenterX, to.Top);
                sb.AppendLine($"  <line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" " +
                               "stroke=\"#6366f1\" stroke-width=\"1.5\" marker-end=\"url(#arrowhead)\" " +
                               "stroke-opacity=\"0.6\"/>");
            }
        }

        // Ярусные метки
        foreach (var tier in plan.Tiers)
        {
            if (tier.Steps.Count == 0) continue;
            var first = positions[tier.Steps[0].Id];
            sb.AppendLine($"  <text x=\"12\" y=\"{first.Top + NodeHeight / 2 + 5}\" " +
                           "font-size=\"11\" fill=\"#94a3b8\" font-weight=\"600\">" +
                           $"T{tier.Level}</text>");
        }

        // Узлы
        foreach (var step in plan.Steps)
        {
            if (!positions.TryGetValue(step.Id, out var pos)) continue;
            var fillColor = step.ToolName != null ? "#eef2ff" : "#f8fafc";
            var borderColor = step.ToolName != null ? "#6366f1" : "#cbd5e1";

            sb.AppendLine($"  <rect x=\"{pos.X}\" y=\"{pos.Y}\" width=\"{NodeWidth}\" height=\"{NodeHeight}\" " +
                           $"rx=\"8\" ry=\"8\" fill=\"{fillColor}\" stroke=\"{borderColor}\" stroke-width=\"1.5\"/>");

            var label = Truncate(step.Description, 28);
            sb.AppendLine($"  <text x=\"{pos.CenterX}\" y=\"{pos.Y + 22}\" " +
                           "text-anchor=\"middle\" font-size=\"12\" font-weight=\"500\" fill=\"#1e293b\">" +
                           $"{Escape(label)}</text>");

            var sub = step.ToolName != null ? $"[{step.ToolName}]" : step.Id;
            sb.AppendLine($"  <text x=\"{pos.CenterX}\" y=\"{pos.Y + 39}\" " +
                           "text-anchor=\"middle\" font-size=\"10\" fill=\"#64748b\">" +
                           $"{Escape(sub)}</text>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    #endregion

    #region Mermaid

    /// <summary>
    /// Генерирует Mermaid-диаграмму flowchart для плана.
    /// Возвращает HTML-блок с классом mermaid для автоматического рендеринга.
    /// </summary>
    public static string ToMermaid(PlanTree plan)
    {
        if (plan == null || plan.Steps.Count == 0)
            return "<div class=\"mermaid\">flowchart TD\n  empty[\"Пустой план\"]</div>";

        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"mermaid\">");
        sb.AppendLine("flowchart TD");

        foreach (var tier in plan.Tiers)
        {
            sb.AppendLine($"  subgraph tier{tier.Level} [\"Ярус {tier.Level}\"]");
            foreach (var step in tier.Steps)
            {
                var label = step.ToolName != null
                    ? $"{step.Description}<br/>[{step.ToolName}]"
                    : step.Description;
                sb.AppendLine($"    {SafeId(step.Id)}[\"{Escape(label)}\"]");
            }
            sb.AppendLine("  end");
        }

        foreach (var step in plan.Steps)
        {
            foreach (var dep in step.DependsOn)
                sb.AppendLine($"  {SafeId(dep)} --> {SafeId(step.Id)}");
        }

        sb.AppendLine("</div>");
        return sb.ToString();
    }

    #endregion

    #region Текстовое дерево

    /// <summary>
    /// Генерирует текстовое представление дерева ярусов.
    /// </summary>
    public static string ToText(PlanTree plan)
    {
        if (plan == null || plan.Steps.Count == 0)
            return "(пустой план)";

        var sb = new StringBuilder();
        sb.AppendLine($"Plan: {plan.Goal}");
        sb.AppendLine($"Steps: {plan.Steps.Count}, Tiers: {plan.Depth}");
        sb.AppendLine();

        foreach (var tier in plan.Tiers)
        {
            sb.AppendLine($"+--- Tier {tier.Level} ({tier.Steps.Count} parallel) ---");
            foreach (var step in tier.Steps)
            {
                var tool = step.ToolName != null ? $" [{step.ToolName}]" : "";
                var deps = step.DependsOn.Count > 0 ? $" <- {string.Join(", ", step.DependsOn)}" : "";
                sb.AppendLine($"|  {step.Id}: {step.Description}{tool}{deps}");
            }
            sb.AppendLine("+--------------------------------");
        }

        return sb.ToString();
    }

    #endregion

    #region Данные для графовой визуализации

    /// <summary>
    /// Извлекает структурированные данные из PlanTree для построения графа
    /// в AI.Charts.Data.GraphData, PlotlyBuilder.AddDirectedGraph и т.д.
    /// </summary>
    /// <returns>
    /// steps — массив (id, label, subtitle, tier);
    /// edges — список (from, to) по зависимостям.
    /// </returns>
    public static (
        (string id, string label, string subtitle, int tier)[] steps,
        (string from, string to)[] edges
    ) ExtractGraphLayout(PlanTree plan)
    {
        if (plan == null || plan.Steps.Count == 0)
            return ([], []);

        var steps = new (string id, string label, string subtitle, int tier)[plan.Steps.Count];
        var edges = new List<(string from, string to)>();

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var s = plan.Steps[i];
            steps[i] = (s.Id, s.Description, s.ToolName != null ? $"[{s.ToolName}]" : null, s.Tier);
            foreach (var d in s.DependsOn)
                edges.Add((d, s.Id));
        }

        return (steps, edges.ToArray());
    }

    #endregion

    #region Layout

    private static Dictionary<string, NodePosition> CalculateLayout(PlanTree plan)
    {
        var positions = new Dictionary<string, NodePosition>(StringComparer.OrdinalIgnoreCase);

        foreach (var tier in plan.Tiers)
        {
            int tierWidth = tier.Steps.Count * (NodeWidth + NodeGap) - NodeGap;
            int startX = PaddingX + (tierWidth > 0 ? 0 : 0);

            for (int i = 0; i < tier.Steps.Count; i++)
            {
                var step = tier.Steps[i];
                int x = PaddingX + i * (NodeWidth + NodeGap);
                int y = PaddingY + tier.Level * (NodeHeight + TierGap);
                positions[step.Id] = new NodePosition(x, y);
            }
        }

        return positions;
    }

    private static (int Width, int Height) CalculateCanvasSize(Dictionary<string, NodePosition> positions)
    {
        int maxX = 0, maxY = 0;
        foreach (var p in positions.Values)
        {
            if (p.Right > maxX) maxX = p.Right;
            if (p.Bottom > maxY) maxY = p.Bottom;
        }
        return (maxX + PaddingX, maxY + PaddingY);
    }

    private readonly record struct NodePosition(int X, int Y)
    {
        public int CenterX => X + NodeWidth / 2;
        public int CenterY => Y + NodeHeight / 2;
        public int Right => X + NodeWidth;
        public int Bottom => Y + NodeHeight;
        public int Top => Y;
    }

    #endregion

    #region Helpers

    private static string Truncate(string text, int max)
        => text != null && text.Length > max ? text[..(max - 1)] + "…" : text ?? "";

    private static string Escape(string text)
        => text?.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;") ?? "";

    private static string SafeId(string id)
        => id?.Replace("-", "_").Replace(" ", "_") ?? "unknown";

    #endregion
}
