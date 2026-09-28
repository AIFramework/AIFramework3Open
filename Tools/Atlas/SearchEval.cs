using System.Text;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Замер поиска на эталоне: доля вопросов с правильным ответом в первых 1, 5 и 10 и MRR@10.
/// </summary>
/// <remarks>
/// Вопрос, на который в индексе нет ни одной подходящей единицы (тип закрытый, переименован
/// или вне библиотеки), в счёт не идёт и считается отдельно: иначе метрика мерила бы полноту
/// эталона, а не качество поиска.
/// </remarks>
public static class SearchEval
{
    /// <summary>Метрики одной ступени на одном источнике.</summary>
    public sealed record Metrics(string Stage, string Source, int Answerable, double At1, double At5, double At10, double Mrr);

    /// <summary>Проверяемая ступень: имя и функция «запрос → выдача».</summary>
    public sealed record Stage(string Name, Func<string, Task<IReadOnlyList<SearchHit>>> Search);

    /// <summary>Считает метрики каждой ступени по каждому источнику и в целом.</summary>
    public static async Task<(IReadOnlyList<Metrics> Rows, int Unanswerable, IReadOnlyList<(GoldItem Item, string Stage)> Misses)> RunAsync(
        IReadOnlyList<GoldItem> gold, IReadOnlyList<CodeUnit> units, IReadOnlyList<Stage> stages)
    {
        string[] types = [.. units.Select(GoldSet.TypeOf).Distinct(StringComparer.Ordinal)];
        GoldItem[] answerable = [.. gold.Where(item => types.Any(type => GoldSet.Matches(type, item)))];
        var rows = new List<Metrics>();
        var misses = new List<(GoldItem, string)>();

        foreach (Stage stage in stages)
        {
            var ranks = new Dictionary<GoldItem, int?>();

            foreach (GoldItem item in answerable)
            {
                IReadOnlyList<SearchHit> hits = await stage.Search(item.Query);
                int index = hits.Take(10).ToList().FindIndex(hit => GoldSet.Matches(hit.Unit, item));
                ranks[item] = index >= 0 ? index + 1 : null;
                if (index < 0) misses.Add((item, stage.Name));
            }

            foreach (string source in answerable.Select(item => item.Source).Distinct().Append("без имени").Append("всего"))
            {
                int?[] group = [.. answerable.Where(item => source switch
                {
                    "всего" => true,
                    "без имени" => !item.NamesAnswer,
                    _ => item.Source == source,
                }).Select(item => ranks[item])];
                rows.Add(new Metrics(stage.Name, source, group.Length,
                    Share(group, 1), Share(group, 5), Share(group, 10),
                    group.Average(rank => rank is int r ? 1.0 / r : 0)));
            }
        }

        return (rows, gold.Count - answerable.Length, misses);
    }

    /// <summary>Таблица для консоли.</summary>
    public static string Format(IReadOnlyList<Metrics> rows)
    {
        var builder = new StringBuilder().AppendLine($"{"ступень",-24}{"источник",-10}{"вопросов",9}{"@1",8}{"@5",8}{"@10",8}{"MRR",8}");

        foreach (Metrics row in rows)
            builder.AppendLine($"{row.Stage,-24}{row.Source,-10}{row.Answerable,9}{row.At1,8:P0}{row.At5,8:P0}{row.At10,8:P0}{row.Mrr,8:F3}");

        return builder.ToString().TrimEnd();
    }

    private static double Share(int?[] ranks, int k) => ranks.Count(rank => rank <= k) / (double)ranks.Length;
}
