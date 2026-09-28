using System.Text;
using System.Text.Json;

namespace AiFramework.Tools.Atlas;

/// <summary>Находка в снимке аудита.</summary>
/// <param name="A">Первый метод: «сборка|id».</param>
/// <param name="B">Второй метод: «сборка|id».</param>
/// <param name="Verdict">Вердикт по коду.</param>
/// <param name="Outcome">Итог исполнения (<see cref="DupReport.Outcome"/>), пусто — не исполнялась.</param>
public sealed record AuditEntry(string A, string B, string Verdict, string Outcome)
{
    /// <summary>Ключ пары, не зависящий от порядка.</summary>
    public string Key => string.CompareOrdinal(A, B) <= 0 ? A + " ↔ " + B : B + " ↔ " + A;
}

/// <summary>Что изменилось между двумя аудитами.</summary>
public sealed record AuditDiff(IReadOnlyList<AuditEntry> Added, IReadOnlyList<(AuditEntry Before, AuditEntry Now)> Changed, IReadOnlyList<AuditEntry> Gone)
{
    /// <summary>Изменений нет.</summary>
    public bool IsEmpty => Added.Count == 0 && Changed.Count == 0 && Gone.Count == 0;
}

/// <summary>
/// Цикл гигиены: индекс, все ступени без сети (детектор, исполнение из кэша), сверка с
/// журналом решений и с прошлым аудитом. Отчёт показывает только перемены: новые дубли,
/// переоткрытые решения, новые расхождения и исчезнувшие пары. Исчезнувшая пара, оба метода
/// которой живы, — обычно слияние (один стал вызывать другой): исполнение проверяет, что
/// выходы остались прежними.
/// </summary>
public static class Audit
{
    /// <summary>Сравнивает два снимка.</summary>
    public static AuditDiff Compare(IReadOnlyDictionary<string, AuditEntry> previous, IReadOnlyDictionary<string, AuditEntry> current) => new(
        [.. current.Where(pair => !previous.ContainsKey(pair.Key)).Select(pair => pair.Value)],
        [.. current.Where(pair => previous.TryGetValue(pair.Key, out AuditEntry? before) && (before.Verdict != pair.Value.Verdict || before.Outcome != pair.Value.Outcome))
            .Select(pair => (previous[pair.Key], pair.Value))],
        [.. previous.Where(pair => !current.ContainsKey(pair.Key)).Select(pair => pair.Value)]);

    /// <summary>Аудит по индексу, открытому в <paramref name="context"/>; снимок — в его папке кэша.</summary>
    public static async Task<string> RunAsync(AtlasContext context, IProgress<int>? progress = null)
    {
        DupFinding[] findings = [.. context.Dups.FindAll()
            .Where(finding => context.Project is null || ProjectReport.Direction(finding, context.Project.Owns) != null)];

        var current = new Dictionary<string, AuditEntry>(StringComparer.Ordinal);
        var byKey = new Dictionary<string, DupFinding>(StringComparer.Ordinal);
        foreach (DupFinding finding in findings)
        {
            string outcome = finding.A.Kind == "method" && finding.B.Kind == "method"
                ? DupReport.Outcome(finding, await DupReport.RunAsync(context, finding.A, finding.B))
                : "";
            var entry = new AuditEntry(Ref(finding.A), Ref(finding.B), finding.Verdict, outcome);
            current[entry.Key] = entry;
            byKey[entry.Key] = finding;
            progress?.Report(current.Count);
        }

        context.BehaviorResults.Save();

        string path = Path.Combine(context.CacheFolder, "audit.json");
        Dictionary<string, AuditEntry>? previous = Load(path);
        Save(path, current);

        DupFinding[] reopened = [.. findings.Where(finding => context.Ledger.IsReopened(finding.A, finding.B))];
        var builder = new StringBuilder().AppendLine($"Аудит {DateTimeOffset.Now:yyyy-MM-dd HH:mm}: находок {current.Count}, "
            + string.Join(", ", current.Values.Where(e => e.Outcome.Length > 0).GroupBy(e => e.Outcome).Select(g => $"{g.Key} {g.Count()}")));

        if (previous is null)
            return builder.AppendLine("Первый прогон: находки запомнены, дальше отчёт покажет только перемены.").ToString().TrimEnd();

        AuditDiff diff = Compare(previous, current);
        if (diff.IsEmpty && reopened.Length == 0) return builder.AppendLine("Изменений с прошлого аудита нет.").ToString().TrimEnd();

        Section(builder, "Новые находки", diff.Added.Select(entry => Line(entry, byKey)));
        Section(builder, "Новые расхождения и сменившиеся вердикты", diff.Changed.Select(change =>
            $"{Line(change.Now, byKey)}; было: {change.Before.Verdict}{(change.Before.Outcome.Length > 0 ? ", " + change.Before.Outcome : "")}"));
        Section(builder, "Переоткрытые решения журнала: код изменился", reopened.Select(finding =>
            $"{UnitReport.ShortId(finding.A.Id)} ↔ {UnitReport.ShortId(finding.B.Id)}: было «{context.Ledger.Outdated(finding.A, finding.B)!.Verdict}» — {context.Ledger.Outdated(finding.A, finding.B)!.Reason}"));

        var gone = new List<string>();
        foreach (AuditEntry entry in diff.Gone) gone.Add(await GoneAsync(context, entry));
        Section(builder, "Исчезнувшие пары", gone);

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Пара пропала из находок. Оба метода живы — слияние или правка: исполнение проверяет,
    /// что выходы совпадают. Метода нет — удалён, проверять нечем.
    /// </summary>
    private static async Task<string> GoneAsync(AtlasContext context, AuditEntry entry)
    {
        CodeUnit? a = Resolve(context, entry.A), b = Resolve(context, entry.B);
        string pair = $"{Short(entry.A)} ↔ {Short(entry.B)} (было: {entry.Verdict})";

        if (a is null || b is null) return $"{pair}: {(a is null ? Short(entry.A) : Short(entry.B))} удалён — исполнением не проверить";

        BehaviorResult result = await DupReport.RunAsync(context, a, b);
        return result.Verdict switch
        {
            BehaviorProbe.Duplicate => $"{pair}: слияние подтверждено исполнением — выходы совпадают",
            BehaviorProbe.DuplicateExceptEdges => $"{pair}: выходы совпадают, кроме крайних случаев — проверьте их",
            BehaviorProbe.NotChecked => $"{pair}: не проверено — {result.Note}",
            _ => $"{pair}: после правки выходы разные — {DupReport.Behavior(result)}",
        };
    }

    private static CodeUnit? Resolve(AtlasContext context, string reference)
    {
        string id = reference[(reference.IndexOf('|') + 1)..];
        return context.Find(id) is { } unit && unit.Id == id ? unit : null;
    }

    private static string Line(AuditEntry entry, Dictionary<string, DupFinding> byKey) =>
        byKey.TryGetValue(entry.Key, out DupFinding? finding)
            ? $"[{entry.Verdict}{(entry.Outcome.Length > 0 ? "; " + entry.Outcome : "")}] {finding.A.Signature}  {finding.A.File}:{finding.A.Line}  ↔  {finding.B.Signature}  {finding.B.File}:{finding.B.Line}"
            : $"[{entry.Verdict}] {Short(entry.A)} ↔ {Short(entry.B)}";

    private static void Section(StringBuilder builder, string title, IEnumerable<string> lines)
    {
        string[] all = [.. lines];
        if (all.Length == 0) return;

        builder.AppendLine().AppendLine($"{title}: {all.Length}");
        foreach (string line in all) builder.AppendLine("  " + line);
    }

    private static string Ref(CodeUnit unit) => unit.Project + "|" + unit.Id;

    private static string Short(string reference) => UnitReport.ShortId(reference[(reference.IndexOf('|') + 1)..]);

    private static Dictionary<string, AuditEntry>? Load(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<AuditEntry[]>(File.ReadAllText(path))?.ToDictionary(entry => entry.Key, StringComparer.Ordinal) : null;

    private static void Save(string path, Dictionary<string, AuditEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(entries.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal)));
    }
}
