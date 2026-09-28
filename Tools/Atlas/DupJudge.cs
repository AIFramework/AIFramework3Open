using AI.LLM.Core.Abstractions;
using System.Text;
using System.Text.Json;

namespace AiFramework.Tools.Atlas;

/// <summary>Вердикт модели по паре.</summary>
/// <param name="Verdict">Один из <see cref="DupJudge.Verdicts"/>; пусто, если ответ не разобран.</param>
/// <param name="Why">Объяснение модели.</param>
public sealed record DupJudgement(string Verdict, string Why);

/// <summary>
/// Вердикт по серой зоне моделью из <c>verdictModel</c>: пакет доказательств — оба тела,
/// описания и признаки пары.
/// </summary>
/// <remarks>
/// Пороги решают ясные случаи (копия, нет связи). «Похожий код» и «один предмет, разный код»
/// — вопрос: это дубль, расхождение (одно и то же должно считаться, а считается по-разному)
/// или сознательное перекрытие. Модель отвечает одним словом из закрытого списка; ответ
/// вне списка считается неразобранным, а не превращается в вердикт.
/// </remarks>
public sealed class DupJudge(ILLMClient client, string? journal = null)
{
    private const int MaxBodyLines = 60;

    // Журнал читает человек: кириллица остаётся кириллицей, а не \u0422.
    private static readonly JsonSerializerOptions Readable = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Допустимые вердикты модели.</summary>
    public static readonly string[] Verdicts = ["дубль", "расхождение", "перекрытие", "нет связи"];

    /// <summary>Спрашивает модель.</summary>
    /// <param name="finding">Пара.</param>
    /// <param name="behavior">Итог исполнения, если был.</param>
    /// <param name="disclose">Что из каждой единицы можно отправить; <c>null</c> — всё.</param>
    /// <param name="cancellation">Отмена.</param>
    /// <remarks>
    /// Если из одной из единиц ничего отправлять нельзя, модель не спрашивается вовсе: вердикт по
    /// одному методу бессмыслен. Каждый отправленный запрос дописывается в журнал
    /// (<c>%LOCALAPPDATA%\Atlas\outbound.jsonl</c>): что ушло к провайдеру, видно без сети.
    /// </remarks>
    public async Task<DupJudgement> JudgeAsync(DupFinding finding, BehaviorResult? behavior = null, Func<CodeUnit, Disclosure>? disclose = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(finding);
        disclose ??= _ => Disclosure.All;

        if (disclose(finding.A) == Disclosure.None || disclose(finding.B) == Disclosure.None)
            return new DupJudgement("", "не запрошен: код проекта в OpenRouter не отправляется (настройка projects)");

        string prompt = Prompt(finding, behavior, disclose);
        if (journal != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
            await File.AppendAllTextAsync(journal, JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, kind = "вердикт дубля", prompt }, Readable) + "\n", cancellation);
        }

        return Parse(await client.SendAsync(prompt, cancellationToken: cancellation));
    }

    /// <summary>Разбор ответа: JSON с полями verdict и why.</summary>
    internal static DupJudgement Parse(string reply)
    {
        int start = reply.IndexOf('{');
        int end = reply.LastIndexOf('}');
        if (start < 0 || end <= start) return new DupJudgement("", reply.Trim());

        try
        {
            using JsonDocument json = JsonDocument.Parse(reply[start..(end + 1)]);
            string verdict = json.RootElement.TryGetProperty("verdict", out JsonElement v) ? v.GetString()?.Trim().ToLowerInvariant() ?? "" : "";
            string why = json.RootElement.TryGetProperty("why", out JsonElement w) ? w.GetString() ?? "" : "";

            return new DupJudgement(Verdicts.Contains(verdict) ? verdict : "", why);
        }
        catch (JsonException)
        {
            return new DupJudgement("", reply.Trim());
        }
    }

    private static string Prompt(DupFinding finding, BehaviorResult? behavior, Func<CodeUnit, Disclosure> disclose)
    {
        var builder = new StringBuilder()
            .AppendLine("Два метода из библиотеки C#. Реши, что это за пара:")
            .AppendLine("- дубль: одно и то же вычисление написано дважды, результаты совпадают — свести к одной версии;")
            .AppendLine("- расхождение: должны считать одно и то же, но результаты будут отличаться (разные формулы, допуски, крайние случаи);")
            .AppendLine("- перекрытие: общий предмет, но разные задачи или контракты — объединение ухудшит код;")
            .AppendLine("- нет связи: сходство случайное.")
            .AppendLine();

        foreach (CodeUnit unit in new[] { finding.A, finding.B })
        {
            string summary = UnitReport.Summary(unit.Doc);
            string[] lines = unit.Body.Split('\n');

            builder.AppendLine($"=== {unit.Signature}  [{unit.Project}]");
            if (summary.Length > 0) builder.AppendLine("Описание: " + summary);

            if (disclose(unit) != Disclosure.All)
            {
                builder.AppendLine("(тело не показано: код закрытого проекта)");
                continue;
            }

            foreach (string line in lines.Take(MaxBodyLines)) builder.AppendLine(line.TrimEnd());
            if (lines.Length > MaxBodyLines) builder.AppendLine($"… ещё {lines.Length - MaxBodyLines} строк");
            builder.AppendLine();
        }

        DupFeatures f = finding.Features;
        builder.AppendLine($"Признаки: сходство кода {f.Code:F2}, описаний {f.Doc:F2}, вызовов {f.Calls:F2}, констант {f.Constants:F2}.");

        if (behavior is { Verdict: not BehaviorProbe.NotChecked })
        {
            builder.AppendLine($"Исполнение на одних входах: {DupReport.Outcome(finding, behavior)}; {DupReport.Behavior(behavior)}. Аргументы B: {behavior.Mapping}.");
            foreach (BehaviorRow row in behavior.Rows) builder.AppendLine($"  {row.Input} → A: {row.A}; B: {row.B}");
            builder.AppendLine("Если на обычных входах выходы различаются, это не дубль.");
        }

        return builder
            .AppendLine("Ответ — только JSON: {\"verdict\":\"дубль|расхождение|перекрытие|нет связи\",\"why\":\"одна-две фразы\"}")
            .ToString();
    }
}
