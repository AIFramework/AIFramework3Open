using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Карточка единицы для человека и модели: сигнатура, описание, где используется, что вызывает.
/// </summary>
public static class UnitReport
{
    private const int ListLimit = 8;

    /// <summary>Области в порядке показа: сначала библиотека, потом места, где её применяют.</summary>
    private static readonly (string Area, string Title)[] Areas =
    [
        ("library", "библиотека"), ("test", "тесты"), ("tutorial", "туториалы"), ("demo", "демо"), ("tool", "утилиты"),
    ];

    /// <summary>Карточка единицы.</summary>
    public static string Describe(UnitStore store, CodeUnit unit)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(unit);

        var builder = new StringBuilder()
            .AppendLine(unit.Signature)
            .AppendLine($"  {unit.Id}  [{unit.Project}, {unit.Access}]")
            .AppendLine($"  {unit.File}:{unit.Line}");

        if (unit.Script != null) builder.AppendLine($"  AIScript: {unit.Script}");

        string summary = Summary(unit.Doc);
        builder.AppendLine(summary.Length > 0 ? $"  {summary}" : "  (нет описания)");

        IReadOnlyList<Caller> callers = store.Callers(unit);
        builder.AppendLine($"Вызывают: {callers.Count}");

        foreach ((string area, string title) in Areas)
        {
            List<Caller> group = [.. callers.Where(caller => caller.Area == area)];
            if (group.Count == 0) continue;

            builder.AppendLine($"  {title} ({group.Count}):");

            foreach (Caller caller in group.Take(ListLimit))
                builder.AppendLine($"    {caller.ShownAs ?? caller.Unit.File}:{caller.Unit.Line}  {ShortId(caller.Unit.Id)}  ×{caller.Count}");

            if (group.Count > ListLimit) builder.AppendLine($"    … ещё {group.Count - ListLimit}");
        }

        if (unit.Kind != "type")
        {
            AppendFlows(builder, "Вход получает от", store.FlowNeighbours(unit, incoming: true));
            AppendFlows(builder, "Результат уходит в", store.FlowNeighbours(unit, incoming: false));

            IReadOnlyList<Callee> callees = store.Callees(unit);
            builder.AppendLine($"Вызывает: {callees.Count}");

            foreach (Callee callee in callees.Take(ListLimit))
                builder.AppendLine($"    {ShortId(callee.Id)}  ×{callee.Count}{(callee.Unit is null ? $"  ({callee.Project})" : "")}");

            if (callees.Count > ListLimit) builder.AppendLine($"    … ещё {callees.Count - ListLimit}");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendFlows(StringBuilder builder, string title, IReadOnlyList<(string Project, string Id, int Count)> flows)
    {
        if (flows.Count == 0) return;

        builder.AppendLine($"{title}: {flows.Count}");
        foreach ((_, string id, int count) in flows.Take(ListLimit)) builder.AppendLine($"    {ShortId(id)}  ×{count}");
        if (flows.Count > ListLimit) builder.AppendLine($"    … ещё {flows.Count - ListLimit}");
    }

    /// <summary>Текст <c>summary</c> одной строкой; пусто, если описания нет.</summary>
    public static string Summary(string doc)
    {
        if (doc.Length == 0) return "";

        try
        {
            XElement? summary = XElement.Parse($"<doc>{doc}</doc>").Element("summary");
            return summary is null ? "" : Regex.Replace(summary.Value, @"\s+", " ").Trim();
        }
        catch (System.Xml.XmlException)
        {
            return "";
        }
    }

    /// <summary>Идентификатор без префикса вида и пространства имён: <c>FFT.CalcFFT(…)</c>.</summary>
    public static string ShortId(string id)
    {
        int colon = id.IndexOf(':');
        string name = colon >= 0 ? id[(colon + 1)..] : id;
        int paren = name.IndexOf('(');
        string path = paren >= 0 ? name[..paren] : name;
        string[] parts = path.Split('.');
        string tail = parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : path;

        return paren >= 0 ? tail + (name.Length - paren > 2 ? "(…)" : "()") : tail;
    }
}
