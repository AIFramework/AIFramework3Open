using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Отчёт «проект ↔ библиотека» в трёх направлениях, у каждой находки сказано, где править:
/// проект повторяет библиотеку, дубль внутри проекта, общий код проекта — кандидат в библиотеку.
/// </summary>
public static class ProjectReport
{
    /// <summary>Метод проекта повторяет библиотечный: вызвать библиотечный.</summary>
    public const string Repeats = "проект повторяет библиотеку";

    /// <summary>Дубль между методами проекта.</summary>
    public const string Internal = "дубль внутри проекта";

    /// <summary>Общий код проекта, которого в библиотеке нет.</summary>
    public const string Candidate = "кандидат в библиотеку";

    /// <summary>Общий код проекта, для которого поиск нашёл похожее в библиотеке.</summary>
    public const string Lookalike = "похожее уже есть в библиотеке";

    private static readonly Regex TypeName = new(@"[\w.]+", RegexOptions.Compiled);

    // Типы общего кода: простые данные, коллекции и типы самой библиотеки.
    private static readonly HashSet<string> SimpleTypes =
        ["string", "int", "long", "short", "byte", "double", "float", "decimal", "bool", "char", "object", "void", "T",
         "System.DateTime", "System.TimeSpan", "System.Guid", "System.ReadOnlySpan", "System.Span", "System.Func", "System.Action", "System.ValueTuple"];

    // Вызовы общего кода: базовая библиотека .NET без файлов, сети, процессов, баз и сериализации.
    private static readonly string[] AllowedCalls = ["M:System.", "M:AI."];
    private static readonly string[] ForbiddenCalls =
        ["M:System.IO.", "M:System.Net.", "M:System.Security.", "M:System.Diagnostics.", "M:System.Data.", "M:System.Text.Json.", "M:System.Threading.", "M:System.Environment."];

    /// <summary>
    /// Направление находки; <c>null</c> — обе стороны в библиотеке (это отчёт самой библиотеки),
    /// библиотечная сторона закрыта (проект не может её вызвать) или пару проекта с библиотекой
    /// связывает только форма короткого кода: цикл суммы похож на любой цикл, а на замену
    /// библиотечным методом нужно одно имя, одно описание или почти тот же код.
    /// </summary>
    public static string? Direction(DupFinding finding, Func<string, bool> owns)
    {
        bool a = owns(finding.A.Project), b = owns(finding.B.Project);
        if (a && b) return Internal;
        if (a == b || (a ? finding.B : finding.A).Access != "public") return null;

        DupFeatures f = finding.Features;
        return f.Name >= 0.99 || f.Doc >= 0.5 || f.Code >= 0.95 ? Repeats : null;
    }

    /// <summary>
    /// Общий код проекта: статические синхронные методы с нетривиальным телом над простыми
    /// данными (числа, строки, коллекции, типы библиотеки), которые не трогают файлы, сеть,
    /// процессы и базы и не вызывают код проекта, кроме такого же общего.
    /// </summary>
    /// <param name="methods">Методы проекта с телами.</param>
    /// <param name="callees">Кого вызывает каждый метод: «сборка|id».</param>
    /// <param name="owns">Сборка принадлежит проекту.</param>
    public static IReadOnlyList<CodeUnit> Candidates(
        IReadOnlyList<CodeUnit> methods, IReadOnlyDictionary<(string, string), HashSet<string>> callees, Func<string, bool> owns)
    {
        Dictionary<string, CodeUnit> pool = methods
            .Where(unit => unit.Kind == "method" && unit.IsStatic
                && CodeShapes.Of(unit.Body) is { Tokens: >= DupDetector.MinTokens } shape && !shape.IsText
                && TypeName.Matches(unit.Inputs + " " + unit.Returns).All(match => IsSimple(match.Value)))
            .ToDictionary(unit => unit.Project + "|" + unit.Id);

        // Метод, зовущий код проекта, общим не считается, пока тот код сам не общий: до неподвижной точки.
        for (bool changed = true; changed;)
        {
            changed = false;
            foreach ((string key, CodeUnit unit) in pool.ToList())
            {
                if (!callees.TryGetValue((unit.Project, unit.Id), out HashSet<string>? calls)) continue;
                if (calls.Any(call => !CallAllowed(call, owns, pool))) changed |= pool.Remove(key);
            }
        }

        return [.. pool.Values.OrderBy(unit => unit.Project, StringComparer.Ordinal).ThenBy(unit => unit.Id, StringComparer.Ordinal)];
    }

    /// <summary>Строит отчёт по проекту, открытому в <paramref name="context"/>.</summary>
    public static async Task<string> BuildAsync(AtlasContext context, int limit, bool behavior = false, TimeSpan? budget = null)
    {
        ProjectSide project = context.Project ?? throw new InvalidOperationException("Открыт репозиторий библиотеки, а не проект на ней: отчёт по нему — dup_report без проекта.");
        var clock = Stopwatch.StartNew();

        (DupFinding Finding, string Direction)[] found = [.. context.Dups.FindAll()
            .Select(finding => (Finding: Oriented(finding, project), Direction: Direction(finding, project.Owns)))
            .Where(pair => pair.Direction != null && context.Ledger.Find(pair.Finding.A, pair.Finding.B) is null)
            .Select(pair => (pair.Finding, pair.Direction!))];

        var repeated = found.Where(pair => pair.Direction == Repeats).Select(pair => pair.Finding.A.Id).ToHashSet();
        var callees = project.Store.CalleeSets();
        CodeUnit[] candidates = [.. Candidates(project.Store.LibraryMethods(), callees, project.Owns)
            .Where(unit => !repeated.Contains(unit.Id))];

        var usage = project.Store.UsageCounts();
        var placed = new List<(CodeUnit Unit, string Kind, CodeUnit? Near, double Coverage, string Place)>();
        foreach (CodeUnit unit in candidates.OrderByDescending(unit => usage.GetValueOrDefault((unit.Project, unit.Id))))
        {
            string query = ApiSearch.NameText(unit) + " " + UnitReport.Summary(unit.Doc);
            SearchHit? near = (await context.Search.SearchAsync(query, new SearchOptions(Limit: 3))).FirstOrDefault();
            double coverage = near is null ? 0 : context.Search.Coverage(query, near.Unit);
            placed.Add((unit, coverage >= StackBuilder.DefaultCoverage ? Lookalike : Candidate, near?.Unit, coverage, Place(unit, callees, near?.Unit, coverage)));
        }

        var behaviours = new Dictionary<DupFinding, BehaviorResult>();
        if (behavior)
        {
            foreach ((DupFinding finding, _) in found.Where(pair => pair.Finding.A.Kind == "method" && pair.Finding.B.Kind == "method"))
            {
                if (budget is { } time && clock.Elapsed > time) break;
                behaviours[finding] = await DupReport.RunAsync(context, finding.A, finding.B);
            }
            context.BehaviorResults.Save();
        }

        var builder = new StringBuilder()
            .AppendLine(project.Describe())
            .AppendLine(LibrarySnapshot.IsCurrent(project.Link) ? "Снимок библиотеки актуален." : "Снимок библиотеки устарел: состояние библиотеки изменилось — atlas project обновит его.")
            .AppendLine($"Итого за {clock.Elapsed.TotalSeconds:F1} с: {Repeats} {found.Count(p => p.Direction == Repeats)}, {Internal} {found.Count(p => p.Direction == Internal)}, "
                + $"{Candidate} {placed.Count(p => p.Kind == Candidate)}, {Lookalike} {placed.Count(p => p.Kind == Lookalike)}.");

        Section(builder, "1. Проект повторяет библиотеку — править в проекте: вызвать библиотечное",
            found.Where(p => p.Direction == Repeats).Select(p => p.Finding), limit, (b, f) =>
            {
                b.AppendLine($"[{f.Verdict}] {f.A.Signature}  [{f.A.Project}]  {f.A.File}:{f.A.Line}")
                    .AppendLine($"    библиотека: {f.B.Signature}  [{f.B.Project}]")
                    .AppendLine($"    что делать: в проекте заменить вызовом {UnitReport.ShortId(f.B.Id)} из {f.B.Project}"
                        + (project.Link.Projects.Contains(f.A.Project) ? "" : $" — но {f.A.Project} не ссылается на библиотеку: понадобится ссылка на {f.B.Project}, иначе оставить и записать перекрытие")
                        + $"; {Features(f)}");
                Behaviour(b, context, f, behaviours);
            });

        Section(builder, "2. Дубли внутри проекта — править в проекте: свести к одной версии",
            found.Where(p => p.Direction == Internal).Select(p => p.Finding), limit, (b, f) =>
            {
                bool general = candidates.Any(c => c.Id == f.A.Id) && candidates.Any(c => c.Id == f.B.Id);
                b.AppendLine($"[{f.Verdict}] {f.A.Signature}  {f.A.File}:{f.A.Line}")
                    .AppendLine($"    ↔ {f.B.Signature}  {f.B.File}:{f.B.Line}")
                    .AppendLine($"    что делать: {f.Advice}; оставить {UnitReport.ShortId(f.Canonical.Id)}"
                        + (general ? "; код общий — лучше вынести в библиотеку и вызвать оттуда" : "") + $"; {Features(f)}");
                Behaviour(b, context, f, behaviours);
            });

        Section(builder, "3. Общий код проекта — кандидаты в библиотеку (или проверить похожее в ней)",
            placed.OrderBy(p => p.Kind == Lookalike ? 0 : 1), limit, (b, p) =>
            {
                int callers = usage.GetValueOrDefault((p.Unit.Project, p.Unit.Id));
                b.AppendLine($"[{p.Kind}] {p.Unit.Signature}  [{p.Unit.Project}]  {p.Unit.File}:{p.Unit.Line}; вызовов в проекте: {callers}");
                b.AppendLine(p.Kind == Lookalike
                    ? $"    в библиотеке, похоже, есть: {p.Near!.Signature}  [{p.Near.Project}] (покрытие слов {p.Coverage:P0}); что делать: в проекте проверить, подходит ли библиотечное"
                    : $"    что делать: в библиотеке добавить ({p.Place}), затем в проекте вызвать оттуда");
            });

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Куда положить кандидата: в сборку библиотеки, которую он уже вызывает; иначе рядом с
    /// ближайшим по словам, если слова совпали хотя бы на треть; иначе — выбрать вручную.
    /// </summary>
    private static string Place(CodeUnit unit, IReadOnlyDictionary<(string, string), HashSet<string>> callees, CodeUnit? near, double coverage)
    {
        string? uses = callees.GetValueOrDefault((unit.Project, unit.Id))?
            .Select(call => call[..call.IndexOf('|')]).Where(LibraryLinks.IsLibrary)
            .GroupBy(assembly => assembly).OrderByDescending(group => group.Count()).Select(group => group.Key).FirstOrDefault();

        return uses != null ? $"в {uses}: метод уже опирается на неё"
            : near != null && coverage >= 0.3 ? $"рядом с {Owner(near)} [{near.Project}]"
            : "место выбрать вручную: близкого по словам в библиотеке нет";
    }

    private static bool IsSimple(string type) =>
        SimpleTypes.Contains(type) || type.StartsWith("System.Collections.Generic.", StringComparison.Ordinal)
        || (type.StartsWith("AI.", StringComparison.Ordinal) && !type.Contains("Tests", StringComparison.Ordinal));

    private static bool CallAllowed(string call, Func<string, bool> owns, Dictionary<string, CodeUnit> pool)
    {
        string id = call[(call.IndexOf('|') + 1)..];
        if (owns(call[..call.IndexOf('|')])) return pool.ContainsKey(call);
        return AllowedCalls.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))
            && !ForbiddenCalls.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>Метод проекта — первым: в отчёте сторона проекта всегда слева.</summary>
    private static DupFinding Oriented(DupFinding finding, ProjectSide project) =>
        !project.Owns(finding.A.Project) && project.Owns(finding.B.Project) ? finding with { A = finding.B, B = finding.A } : finding;

    private static void Section<T>(StringBuilder builder, string title, IEnumerable<T> items, int limit, Action<StringBuilder, T> append)
    {
        T[] all = [.. items];
        builder.AppendLine().AppendLine($"{title}: {all.Length}");
        foreach (T item in all.Take(limit)) append(builder, item);
        if (all.Length > limit) builder.AppendLine($"… ещё {all.Length - limit}");
    }

    private static void Behaviour(StringBuilder builder, AtlasContext context, DupFinding finding, Dictionary<DupFinding, BehaviorResult> behaviours)
    {
        if (behaviours.TryGetValue(finding, out BehaviorResult? result))
            builder.AppendLine($"    исполнение: {DupReport.Outcome(finding, result)}; {DupReport.Behavior(result)}");
    }

    private static string Features(DupFinding f) =>
        $"код {f.Features.Code:F2}, описание {f.Features.Doc:F2}, имя {f.Features.Name:F2}, вызовы {f.Features.Calls:F2}";

    private static string Owner(CodeUnit unit) =>
        unit.Kind == "type" ? UnitReport.ShortId(unit.Id) : string.Join('.', ApiSearch.QualifiedName(unit.Id).Split('.').SkipLast(1).TakeLast(1));
}
