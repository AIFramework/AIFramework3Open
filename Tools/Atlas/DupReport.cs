using System.Diagnostics;
using System.Text;

namespace AiFramework.Tools.Atlas;

/// <summary>Отчёты по дублям для консоли и MCP.</summary>
public static class DupReport
{

    /// <summary>
    /// Отчёт по всей библиотеке: новые находки и переоткрытые решения, без уже решённого.
    /// С исполнением пары методов прогоняются в рабочем процессе, и первыми идут расхождения.
    /// </summary>
    /// <param name="context">Контекст репозитория.</param>
    /// <param name="limit">Сколько находок показать.</param>
    /// <param name="behavior">Исполнять пары.</param>
    /// <param name="progress">Сколько пар исполнено.</param>
    /// <param name="budget">Сколько времени тратить на пары, которых нет в кэше; остальные — при следующем вызове.</param>
    public static async Task<string> LibraryAsync(AtlasContext context, int limit, bool behavior = false, IProgress<int>? progress = null, TimeSpan? budget = null)
    {
        var clock = Stopwatch.StartNew();
        DupDetector detector = context.Dups;
        IReadOnlyList<DupFinding> all = detector.FindAll();
        TimeSpan elapsed = clock.Elapsed;

        DupFinding[] decided = [.. all.Where(finding => context.Ledger.Find(finding.A, finding.B) != null)];
        DupFinding[] open = [.. all.Except(decided)];

        var builder = new StringBuilder()
            .AppendLine($"Методов под наблюдением: {detector.Count}; находок: {all.Count} ({elapsed.TotalSeconds:F1} с); решено в журнале: {decided.Length}")
            .AppendLine("По вердиктам: " + string.Join(", ", open.GroupBy(f => f.Verdict).OrderBy(g => DupDetector.Severity(g.Key)).Select(g => $"{g.Key} {g.Count()}")))
            .AppendLine("По источникам: " + string.Join(", ", open.SelectMany(f => f.Sources.Split('+')).GroupBy(s => s).Select(g => $"{g.Key} {g.Count()}")));

        var behaviours = new Dictionary<DupFinding, BehaviorResult>();
        if (behavior)
        {
            clock.Restart();
            int fresh = 0, postponed = 0;
            foreach (DupFinding finding in open.Where(f => f.A.Kind == "method" && f.B.Kind == "method"))
            {
                if (budget is { } limitTime && clock.Elapsed > limitTime && !Known(context, finding.A, finding.B))
                {
                    postponed++;
                    continue;
                }

                fresh += Known(context, finding.A, finding.B) ? 0 : 1;
                behaviours[finding] = await RunAsync(context, finding.A, finding.B);
                progress?.Report(behaviours.Count);
            }

            context.BehaviorResults.Save();
            builder.AppendLine($"Исполнено пар: {behaviours.Count}, из них заново {fresh} ({clock.Elapsed.TotalSeconds:F0} с)"
                + (postponed > 0 ? $"; отложено {postponed}: не хватило времени, повторите вызов" : "") + ": "
                + string.Join(", ", behaviours.Select(p => Outcome(p.Key, p.Value)).GroupBy(v => v).OrderBy(g => BehaviorRank(g.Key)).Select(g => $"{g.Key} {g.Count()}")));
            open = [.. open.OrderBy(f => behaviours.TryGetValue(f, out BehaviorResult? r) ? BehaviorRank(Outcome(f, r)) : BehaviorRank(""))];
        }

        foreach (DupFinding finding in open.Take(limit))
        {
            builder.AppendLine();
            Append(builder, finding, context.Ledger.IsReopened(finding.A, finding.B) ? "решение переоткрыто: код изменился" : null);
            if (behaviours.TryGetValue(finding, out BehaviorResult? result)) AppendBehavior(builder, context, finding, result, "    ");
        }

        if (open.Length > limit) builder.AppendLine().AppendLine($"… ещё {open.Length - limit}");

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Исполняет пару в рабочем процессе или берёт итог из кэша; проект не собран — пара не
    /// проверена. Кэш записывает на диск вызывающий.
    /// </summary>
    public static async Task<BehaviorResult> RunAsync(AtlasContext context, CodeUnit a, CodeUnit b)
    {
        string? first = context.AssemblyPath(a), second = context.AssemblyPath(b);
        if (first is null || second is null) return BehaviorResult.Unchecked($"проект не собран: {(first is null ? a.Project : b.Project)}");

        string build = Build(first, second);
        if (context.BehaviorResults.TryGet(a, b, build, out BehaviorResult cached)) return cached;

        BehaviorResult result = await context.Behavior.CompareAsync(new MethodRef(first, a.Id), new MethodRef(second, b.Id));
        context.BehaviorResults.Put(a, b, build, result);
        return result;
    }

    /// <summary>Итог пары известен без исполнения: он в кэше или проект не собран.</summary>
    private static bool Known(AtlasContext context, CodeUnit a, CodeUnit b) =>
        context.AssemblyPath(a) is not string first || context.AssemblyPath(b) is not string second
        || context.BehaviorResults.TryGet(a, b, Build(first, second), out _);

    private static string Build(string first, string second) =>
        $"{File.GetLastWriteTimeUtc(first).Ticks}|{File.GetLastWriteTimeUtc(second).Ticks}";

    /// <summary>Разбор пары: признаки, вердикт, решение журнала, вердикт модели, начала тел.</summary>
    public static async Task<string> ExplainAsync(AtlasContext context, string a, string b)
    {
        string report = Explain(context, a, b, out DupFinding? finding);
        if (finding is null) return report;

        BehaviorResult behavior = await RunAsync(context, finding.A, finding.B);
        context.BehaviorResults.Save();
        var builder = new StringBuilder(report).AppendLine().AppendLine();
        AppendBehavior(builder, context, finding, behavior, "");
        report = builder.ToString().TrimEnd();

        if (finding.Verdict is "копия" or DupDetector.Unrelated) return report;

        if (context.Judge is not { } judge) return report + "\n\nВердикт модели: выключен (нет ключа или verdictModel).";

        DupJudgement judgement = await judge.JudgeAsync(finding, behavior, context.Disclose);
        return report + $"\n\nВердикт модели ({context.Settings.VerdictModel}): {(judgement.Verdict.Length > 0 ? judgement.Verdict : "не разобран")} — {judgement.Why}";
    }

    private static string Explain(AtlasContext context, string a, string b, out DupFinding? found)
    {
        found = null;
        CodeUnit? first = context.Find(a);
        CodeUnit? second = context.Find(b);

        if (first is null || second is null) return $"Не найдено: {(first is null ? a : b)}";

        DupFinding? finding = context.Dups.Compare(first, second);
        if (finding is null) return "Сравнивать нечего: у одного из методов нет тела в библиотеке.";
        found = finding;

        var builder = new StringBuilder();
        Append(builder, finding, context.Ledger.Find(first, second) is { } decision
            ? $"в журнале: {decision.Verdict} — {decision.Reason}"
            : null);

        foreach (CodeUnit unit in new[] { first, second })
        {
            string[] lines = unit.Body.Split('\n');
            builder.AppendLine().AppendLine($"{unit.File}:{unit.Line}").AppendLine(unit.Signature);
            foreach (string line in lines.Take(15)) builder.AppendLine("  " + line.TrimEnd());
            if (lines.Length > 15) builder.AppendLine($"  … ещё {lines.Length - 15} строк");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Черновик против библиотеки: детектор по коду, имени и описанию, а по замыслу словами —
    /// ещё и поиск, отфильтрованный покрытием слов замысла (как у шагов стека).
    /// </summary>
    public static async Task<string> CheckAsync(AtlasContext context, string code, string? intent)
    {
        IReadOnlyList<DupFinding> findings = context.Dups.Check(code, intent);
        var seen = findings.Select(finding => finding.B.Id).ToHashSet();

        CodeUnit[] byIntent = await ByIntentAsync(context.Search, intent, seen);
        CodeUnit[] inProject = context.ProjectSearch is { } projectSearch ? await ByIntentAsync(projectSearch, intent, seen) : [];
        string where = context.Project is null ? "в библиотеке" : $"в библиотеке и проекте {context.Project.Name}";

        if (findings.Count == 0 && byIntent.Length == 0 && inProject.Length == 0)
            return $"Похожего {where} не найдено: по коду, имени, описанию и замыслу черновик новый.";

        var builder = new StringBuilder();
        if (findings.Count > 0) builder.AppendLine($"Похожее {where}: {findings.Count}");
        foreach (DupFinding finding in findings)
        {
            builder.AppendLine();
            Append(builder, finding with { A = finding.B, B = finding.A }, null);
        }

        foreach ((string title, CodeUnit[] units) in new[] { ("По замыслу в библиотеке уже есть:", byIntent), ("По замыслу в проекте уже есть:", inProject) })
        {
            if (units.Length > 0) builder.AppendLine().AppendLine(title);
            foreach (CodeUnit unit in units)
                builder.AppendLine($"  {unit.Signature}  [{unit.Project}]  {unit.File}:{unit.Line}").AppendLine($"    {unit.Id}  {UnitReport.Summary(unit.Doc)}");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Поиск по замыслу, отфильтрованный покрытием слов, как у шагов стека.</summary>
    private static async Task<CodeUnit[]> ByIntentAsync(ApiSearch search, string? intent, HashSet<string> seen) =>
        string.IsNullOrWhiteSpace(intent)
            ? []
            : [.. (await search.SearchAsync(intent, new SearchOptions(Limit: 10)))
                .Where(hit => !seen.Contains(hit.Unit.Id) && search.Coverage(intent, hit.Unit) >= StackBuilder.DefaultCoverage)
                .Select(hit => hit.Unit)
                .Take(5)];

    /// <summary>Замер на эталоне <see cref="DupGold"/>: детектор и исполнение каждой пары.</summary>
    public static async Task<string> GoldAsync(AtlasContext context)
    {
        IReadOnlyList<DupFinding> all = context.Dups.FindAll();
        var found = all.Select(f => (f.A.Id, f.B.Id)).ToHashSet();
        var builder = new StringBuilder();
        int positives = 0, caught = 0, negatives = 0, clean = 0;
        var outcomes = new List<(bool Duplicate, string Outcome)>();

        foreach (DupGold.Pair pair in DupGold.Pairs)
        {
            CodeUnit? a = context.Store.Find(pair.A, limit: 1).FirstOrDefault(unit => unit.Id == pair.A);
            CodeUnit? b = context.Store.Find(pair.B, limit: 1).FirstOrDefault(unit => unit.Id == pair.B);

            if (a is null || b is null)
            {
                builder.AppendLine($"? нет в индексе: {(a is null ? pair.A : pair.B)}");
                continue;
            }

            bool inReport = found.Contains((a.Id, b.Id)) || found.Contains((b.Id, a.Id));
            DupFinding? direct = context.Dups.Compare(a, b);
            bool ok = inReport == pair.Duplicate;

            if (pair.Duplicate) { positives++; caught += inReport ? 1 : 0; }
            else { negatives++; clean += inReport ? 0 : 1; }

            BehaviorResult behavior = await RunAsync(context, a, b);
            string outcome = direct is null ? behavior.Verdict : Outcome(direct, behavior);
            outcomes.Add((pair.Duplicate, outcome));

            builder.AppendLine($"{(ok ? "✓" : "✗")} {(pair.Duplicate ? "дубль" : "не дубль")}: {UnitReport.ShortId(a.Id)} ↔ {UnitReport.ShortId(b.Id)} — {pair.Note}")
                .AppendLine($"    в отчёте: {(inReport ? "да" : "нет")}; вердикт пары: {direct?.Verdict ?? "—"}; {Features(direct?.Features)}")
                .AppendLine($"    исполнение: {outcome}; {Behavior(behavior)}");
        }

        foreach (DupDecision seed in context.Ledger.Decisions.Where(decision => decision.HashA == "*"))
        {
            DupFinding[] covered = [.. all.Where(f => context.Ledger.Find(f.A, f.B) == seed)];
            builder.AppendLine($"• журнал: {seed.A} ↔ {seed.B} — «{seed.Verdict}»; находок детектора под этим решением: {covered.Length}"
                + (covered.Length > 0 ? $" (детектор сказал бы: {string.Join(", ", covered.Select(f => f.Verdict).Distinct())})" : ""));
        }

        context.BehaviorResults.Save();
        return builder
            .AppendLine($"Дубли найдены: {caught} из {positives}; ложных находок среди «не дублей»: {negatives - clean} из {negatives}")
            .AppendLine("Исполнение дублей: " + Tally(outcomes.Where(o => o.Duplicate).Select(o => o.Outcome)))
            .AppendLine("Исполнение «не дублей»: " + Tally(outcomes.Where(o => !o.Duplicate).Select(o => o.Outcome)))
            .ToString().TrimEnd();
    }

    /// <summary>Итог исполнения: вердикт, связь, переходник, таблица расходящихся входов, оговорка про общий помощник.</summary>
    private static void AppendBehavior(StringBuilder builder, AtlasContext context, DupFinding finding, BehaviorResult result, string indent)
    {
        builder.AppendLine($"{indent}исполнение: {Outcome(finding, result)}; {Behavior(result)}");
        if (result.Verdict == BehaviorProbe.NotChecked) return;

        builder.AppendLine($"{indent}    аргументы B: {result.Mapping}");
        foreach (BehaviorRow row in result.Rows)
            builder.AppendLine($"{indent}    {(row.Edge ? "крайний случай" : "вход")} {row.Input}").AppendLine($"{indent}      A: {row.A}; B: {row.B}");

        IReadOnlyList<string> common = context.Dups.CommonCalls(finding.A, finding.B);
        if (common.Count > 0 && result.Verdict is BehaviorProbe.Duplicate or BehaviorProbe.DuplicateExceptEdges or BehaviorProbe.Related)
            builder.AppendLine($"{indent}    оговорка: оба вызывают {string.Join(", ", common.Take(3).Select(key => UnitReport.ShortId(key[(key.IndexOf('|') + 1)..])))} — совпадение выходов не доказывает, что верны оба");
    }

    /// <summary>Итог исполнения одной строкой.</summary>
    public static string Behavior(BehaviorResult result) => result.Verdict == BehaviorProbe.NotChecked
        ? result.Note
        : (result.Relation.Length > 0 && result.Relation != OutputRelation.Equal ? $"связь выходов: {result.Relation}; " : "")
            + $"входов {result.Cases}, оба вернули значение на {result.Compared} обычных"
            + (result.Deviation > 0 ? $"; отклонение до {result.Deviation:G2}" : "");

    /// <summary>Одно и то же по имени или описанию, но выходы разные.</summary>
    public const string Divergence = "расхождение";

    /// <summary>
    /// Итог пары по исполнению с учётом того, что обещают имена и описания. Разные выходы у
    /// методов с одним именем или описанием — расхождение: один из них, возможно, неверен.
    /// У методов с разными именами — просто разные функции одного шаблона (Acos и Tanh).
    /// </summary>
    /// <remarks>
    /// Внутри одного типа описания пишутся по шаблону («для каждого элемента матрицы»), там
    /// один предмет — только одно имя. Методы без аргументов возвращают константы своих
    /// классов (<c>ModelName</c>): разные константы расхождением не считаются.
    /// </remarks>
    public static string Outcome(DupFinding finding, BehaviorResult result)
    {
        DupFeatures f = finding.Features;
        bool sameSubject = result.Mapping != BehaviorProbe.NoArguments
            && (f.Name >= 0.99 || (!f.SameType && f.Doc >= 0.6));

        return result.Verdict switch
        {
            BehaviorProbe.Different => sameSubject ? Divergence : "разные функции",
            BehaviorProbe.Related => sameSubject ? "дубль с поправкой" : "связанные функции",
            _ => result.Verdict,
        };
    }

    private static int BehaviorRank(string outcome) => outcome switch
    {
        Divergence => 0,
        BehaviorProbe.DuplicateExceptEdges => 1,
        BehaviorProbe.Duplicate => 2,
        "дубль с поправкой" => 3,
        "связанные функции" => 4,
        "разные функции" => 5,
        BehaviorProbe.NotChecked => 6,
        _ => 7,
    };

    private static string Tally(IEnumerable<string> outcomes) =>
        string.Join(", ", outcomes.GroupBy(o => o).OrderBy(g => BehaviorRank(g.Key)).Select(g => $"{g.Key} {g.Count()}"));

    private static void Append(StringBuilder builder, DupFinding finding, string? note)
    {
        builder.AppendLine($"[{finding.Verdict}] {finding.A.Signature}")
            .AppendLine($"    ↔ {finding.B.Signature}")
            .AppendLine($"    {finding.A.Id}  {finding.A.File}:{finding.A.Line}")
            .AppendLine($"    {finding.B.Id}  {finding.B.File}:{finding.B.Line}")
            .AppendLine($"    {Features(finding.Features)}; источники: {finding.Sources}")
            .AppendLine($"    что делать: {finding.Advice}; каноническая: {UnitReport.ShortId(finding.Canonical.Id)} [{finding.Canonical.Project}]");

        if (note != null) builder.AppendLine($"    {note}");
    }

    private static string Features(DupFeatures? f) => f is null
        ? ""
        : $"код {f.Code:F2}, описание {f.Doc:F2}, имя {f.Name:F2}, вызовы {f.Calls:F2}, константы {f.Constants:F2}, управление {f.Control:F2}"
            + (f.SameInputs ? ", входы те же" : "") + (f.SameType ? ", один тип" : "")
            + (Calibration.Current is { } calibration ? $"; то же поведение с вероятностью {calibration.Probability(f):P0}" : "");
}
