using AI.LLM.Core.Abstractions;
using System.Text;
using System.Text.Json;

namespace AiFramework.Tools.Atlas;

/// <summary>Выбор модели: единица и зачем она нужна задаче.</summary>
public sealed record LlmPick(SearchHit Hit, string Why);

/// <summary>Ответ LLM-ступени.</summary>
/// <param name="Picks">Выбранные единицы в порядке полезности.</param>
/// <param name="Missing">Чего для задачи в выдаче нет, словами модели; пусто — всё нашлось.</param>
/// <param name="InvalidReferences">Сколько ссылок модели не указывало ни на одного кандидата.</param>
/// <param name="Ordered">Кандидаты: выбранные первыми, остальные следом в прежнем порядке.</param>
public sealed record LlmAnswer(IReadOnlyList<LlmPick> Picks, string Missing, int InvalidReferences, IReadOnlyList<SearchHit> Ordered);

/// <summary>
/// LLM-ступень поиска: модель читает кандидатов и отвечает, что решает задачу и чего не хватает.
/// </summary>
/// <remarks>
/// Модель ссылается на кандидатов номерами из списка, а не идентификаторами: номер вне
/// списка отбрасывается и считается в <see cref="LlmAnswer.InvalidReferences"/>, поэтому
/// выдуманный метод в ответ не попадает в принципе.
/// </remarks>
public sealed class LlmSelector(ILLMClient client)
{
    private const int MaxCandidates = 20;

    /// <summary>Спрашивает модель.</summary>
    public async Task<LlmAnswer> SelectAsync(string query, IReadOnlyList<SearchHit> candidates, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        List<SearchHit> shown = [.. candidates.Take(MaxCandidates)];
        string reply = await client.SendAsync(Prompt(query, shown), cancellationToken: cancellation);

        return Parse(reply, shown, candidates);
    }

    /// <summary>Разбор ответа; разобрать не удалось — выбор пуст, порядок прежний.</summary>
    internal static LlmAnswer Parse(string reply, IReadOnlyList<SearchHit> shown, IReadOnlyList<SearchHit> candidates)
    {
        int start = reply.IndexOf('{');
        int end = reply.LastIndexOf('}');

        if (start < 0 || end <= start) return new LlmAnswer([], "", 1, candidates);

        var picks = new List<LlmPick>();
        int invalid = 0;
        string missing = "";

        try
        {
            using JsonDocument json = JsonDocument.Parse(reply[start..(end + 1)]);

            if (json.RootElement.TryGetProperty("missing", out JsonElement gap) && gap.ValueKind == JsonValueKind.String)
                missing = gap.GetString() ?? "";

            if (json.RootElement.TryGetProperty("picks", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement pick in list.EnumerateArray())
                {
                    int n = pick.TryGetProperty("n", out JsonElement number) && number.TryGetInt32(out int value) ? value : 0;
                    string why = pick.TryGetProperty("why", out JsonElement reason) ? reason.GetString() ?? "" : "";

                    if (n < 1 || n > shown.Count || picks.Any(p => p.Hit == shown[n - 1])) invalid++;
                    else picks.Add(new LlmPick(shown[n - 1], why));
                }
            }
        }
        catch (JsonException)
        {
            return new LlmAnswer([], "", 1, candidates);
        }

        List<SearchHit> ordered = [.. picks.Select(pick => pick.Hit), .. candidates.Where(hit => picks.All(pick => pick.Hit != hit))];
        return new LlmAnswer(picks, missing, invalid, ordered);
    }

    private static string Prompt(string query, IReadOnlyList<SearchHit> shown)
    {
        var builder = new StringBuilder()
            .AppendLine("Задача разработчика:")
            .AppendLine(query)
            .AppendLine()
            .AppendLine("Кандидаты из библиотеки AIFramework (C#): номер, сигнатура, описание.");

        for (int i = 0; i < shown.Count; i++)
        {
            string summary = UnitReport.Summary(shown[i].Unit.Doc);
            builder.AppendLine($"[{i + 1}] {shown[i].Unit.Signature}{(summary.Length > 0 ? " — " + summary : "")}");
        }

        return builder
            .AppendLine()
            .AppendLine("Выбери кандидатов, которые решают задачу или её часть, в порядке полезности. Ссылайся только на номера из списка.")
            .AppendLine("Если чего-то для задачи в списке нет, опиши это в поле missing, иначе оставь его пустым.")
            .AppendLine("Ответ — только JSON: {\"picks\":[{\"n\":номер,\"why\":\"зачем, одной фразой\"}],\"missing\":\"...\"}")
            .ToString();
    }
}
