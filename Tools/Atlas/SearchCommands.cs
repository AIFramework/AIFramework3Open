using System.Diagnostics;

namespace AiFramework.Tools.Atlas;

/// <summary>Команды поиска: искать, посчитать векторы, замерить на эталоне.</summary>
internal static class SearchCommands
{
    public static async Task<int> SearchAsync(string query, string folder, bool withLlm)
    {
        using AtlasContext context = AtlasContext.Open(folder);
        Console.WriteLine($"[{await context.EnableVectorsAsync(allowFullRun: false)}]");

        IReadOnlyList<SearchHit> hits = await context.Search.SearchAsync(query, new SearchOptions(Limit: withLlm ? 20 : 10));

        if (hits.Count == 0)
        {
            Console.WriteLine($"По «{query}» ничего не найдено.");
            return 1;
        }

        if (withLlm && context.Llm is { } llm)
        {
            LlmAnswer answer = await llm.SelectAsync(query, hits);

            foreach (LlmPick pick in answer.Picks) Console.WriteLine($"✓ {pick.Hit.Unit.Signature}\n    {pick.Hit.Unit.Id}\n    {pick.Why}");
            if (answer.Missing.Length > 0) Console.WriteLine($"Не хватает: {answer.Missing}");
            if (answer.InvalidReferences > 0) Console.WriteLine($"Отброшено ссылок мимо списка: {answer.InvalidReferences}");

            return 0;
        }

        if (withLlm) Console.WriteLine("[LLM выключена: нет ключа или не задана searchModel]");

        foreach (SearchHit hit in hits.Take(10))
        {
            string summary = UnitReport.Summary(hit.Unit.Doc);
            Console.WriteLine($"{hit.Score,6:F3}  {hit.Unit.Signature}");
            Console.WriteLine($"        {hit.Unit.Id}  [BM25 {hit.LexicalRank?.ToString() ?? "—"}, векторы {hit.VectorRank?.ToString() ?? "—"}]");
            if (summary.Length > 0) Console.WriteLine($"        {(summary.Length > 140 ? summary[..140] + "…" : summary)}");
        }

        return 0;
    }

    /// <summary>Считает векторы всех единиц открытого API; повторный запуск докупает только новые.</summary>
    public static async Task<int> EmbedAsync(string folder)
    {
        using AtlasContext context = AtlasContext.Open(folder);
        var clock = Stopwatch.StartNew();
        var progress = new Progress<int>(done => Console.Error.Write($"\r{done} / {context.Search.Units.Count}"));

        string result = await context.EnableVectorsAsync(allowFullRun: true, progress);
        Console.Error.WriteLine();
        Console.WriteLine($"{result}, {clock.Elapsed.TotalSeconds:F1} с");

        return context.Search.HasVectors ? 0 : 1;
    }

    public static int Glossary(string words, string folder)
    {
        using AtlasContext context = AtlasContext.Open(folder);

        foreach (string token in words.Split(',').SelectMany(CodeTokenizer.Tokens))
            Console.WriteLine($"{token}: {string.Join(", ", context.Search.Glossary.Translations(token).Select(t => $"{t.Term} ({t.Npmi:F2})"))}");

        return 0;
    }

    /// <summary>
    /// Замер поиска на эталоне. Сетевые ступени — только если они включены настройками;
    /// <paramref name="sample"/> прореживает эталон для дорогих прогонов, <paramref name="missesPath"/>
    /// пишет промахи в файл.
    /// </summary>
    public static async Task<int> EvalAsync(string folder, string? missesPath, int? sample)
    {
        using AtlasContext context = AtlasContext.Open(folder);
        ApiSearch search = context.Search;
        string vectors = await context.EnableVectorsAsync(allowFullRun: false);

        Console.WriteLine($"Индекс поиска: {search.Units.Count} единиц; {vectors}; реранкер {(search.HasReranker ? context.Settings.RerankModel : "выключен")}; LLM {context.Settings.SearchModel ?? "выключена"}");

        IReadOnlyList<GoldItem> gold = GoldSet.Load(context.Root);
        if (sample is int n && n > 0 && n < gold.Count) gold = [.. gold.Where((_, i) => i % (gold.Count / n) == 0).Take(n)];

        int invalid = 0;
        var stages = new List<SearchEval.Stage>
        {
            Stage("BM25", new SearchOptions(Vectors: false, Rerank: false, Usage: false)),
            Stage("BM25+обжитость", new SearchOptions(Vectors: false, Rerank: false)),
            Stage("+глоссарий ×0,1", new SearchOptions(Vectors: false, Rerank: false, Glossary: true)),
        };

        if (search.HasVectors)
        {
            stages.Add(Stage("BGE-M3", new SearchOptions(Lexical: false, Rerank: false, Usage: false)));
            stages.Add(Stage("RRF", new SearchOptions(Rerank: false, Usage: false)));
            stages.Add(Stage("RRF+обжитость", new SearchOptions(Rerank: false)));
        }

        if (search.HasReranker) stages.Add(Stage("+реранкер", new SearchOptions()));

        if (context.Llm is { } llm)
        {
            stages.Add(new SearchEval.Stage("+LLM", async query =>
            {
                LlmAnswer answer = await llm.SelectAsync(query, await search.SearchAsync(query, new SearchOptions(Limit: 20)));
                invalid += answer.InvalidReferences;
                return answer.Ordered;
            }));
        }

        var clock = Stopwatch.StartNew();
        var (rows, unanswerable, misses) = await SearchEval.RunAsync(gold, search.Units, stages);

        Console.WriteLine($"Эталон: {gold.Count} вопросов, без ответа в индексе {unanswerable}; прогон {clock.Elapsed.TotalSeconds:F1} с");
        Console.WriteLine(SearchEval.Format(rows));
        if (context.Llm != null) Console.WriteLine($"Ссылок LLM мимо списка кандидатов: {invalid}");

        if (missesPath != null)
        {
            File.WriteAllLines(missesPath, misses.Select(miss => $"{miss.Stage}\t{miss.Item.Source}\t{miss.Item.Name}\t{string.Join(",", miss.Item.Types)}\t{miss.Item.Query}"));
            Console.WriteLine($"Промахи: {missesPath}");
        }

        return 0;

        SearchEval.Stage Stage(string name, SearchOptions options) => new(name, query => search.SearchAsync(query, options));
    }
}
