using System.Diagnostics;

namespace AiFramework.Tools.Atlas;

/// <summary>Команды сборки стека: пути в графе типов, стек под задачу, замер на бенчмарке.</summary>
internal static class StackCommands
{
    public static int Path(string from, string to, string folder)
    {
        using AtlasContext context = AtlasContext.Open(folder);
        TypeGraph graph = context.Graph;

        Console.WriteLine($"Граф типов: {graph.TypeCount} типов, {graph.ArcCount} дуг");

        if (graph.Resolve(from) is not { } source || graph.Resolve(to) is not { } target)
        {
            Console.WriteLine($"Тип не найден: {(graph.Resolve(from) is null ? from : to)}");
            return 1;
        }

        IReadOnlyList<IReadOnlyList<CodeUnit>> paths = graph.Paths(source, target);
        Console.WriteLine($"{source} → {target}: путей {paths.Count}");

        foreach (IReadOnlyList<CodeUnit> path in paths)
            Console.WriteLine("  " + string.Join("  →  ", path.Select(unit => $"{UnitReport.ShortId(unit.Id)}: {graph.DataOutput(unit)}")));

        return paths.Count > 0 ? 0 : 1;
    }

    public static async Task<int> StackAsync(string task, string? steps, bool script, bool candidates, string folder)
    {
        using AtlasContext context = AtlasContext.Open(folder);
        await context.EnableVectorsAsync(allowFullRun: false);

        IReadOnlyList<string>? given = steps is null ? null : [.. steps.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        if (candidates)
        {
            foreach (string step in given ?? TaskSteps.Split(task))
            {
                Console.WriteLine($"«{step}»:");
                foreach ((SearchHit hit, double coverage) in (await context.Stacks(script).CandidatesAsync(step)).Take(8))
                    Console.WriteLine($"    {coverage,5:P0}  {hit.Score,5:F2}  {hit.Unit.Script ?? UnitReport.ShortId(hit.Unit.Id)}  — {UnitReport.Summary(hit.Unit.Doc)}");
            }

            return 0;
        }
        StackAnswer answer = await StackReport.BuildAsync(context, task, given, script);

        Console.WriteLine(StackReport.Format(answer));
        return answer.Check?.Passed == true ? 0 : 1;
    }

    /// <summary>
    /// Задачи бенчмарка AIScript: стек по шагам клиента, проверка лучшего. Доля прошедших
    /// проверку — метрика этапа; шаги с пробелом и без проверки считаются отдельно.
    /// </summary>
    public static async Task<int> EvalAsync(string folder, bool script, double? coverage, bool? glossary)
    {
        using AtlasContext context = AtlasContext.Open(folder);
        await context.EnableVectorsAsync(allowFullRun: false);
        StackBuilder builder = context.Stacks(script, coverage, glossary);
        Console.WriteLine($"Порог покрытия {builder.MinCoverage:P0}, глоссарий {(builder.Glossary ? "включён" : "выключен")}, векторы {(context.Search.HasVectors ? "есть" : "нет")}");

        var clock = Stopwatch.StartNew();
        int passed = 0, failed = 0, unchecked_ = 0, gaps = 0, right = 0, total = 0;

        foreach ((string name, string task, IReadOnlyList<string> steps, IReadOnlyList<string[]> expected) in StackBenchmark.Tasks)
        {
            StackAnswer answer = await StackReport.BuildAsync(context, builder, task, steps, script);
            StackPlan? best = answer.Plans.FirstOrDefault();
            int stepGaps = best?.Gaps.Count() ?? steps.Count;
            gaps += stepGaps;

            string verdict = answer.Check?.Passed switch { true => "пройдена", false => "НЕ пройдена", null => "не выполнена" };
            _ = answer.Check?.Passed switch { true => passed++, false => failed++, null => unchecked_++ };

            Console.WriteLine($"— {name}: проверка {verdict}; пробелов {stepGaps}");
            for (int i = 0; i < (best?.Steps.Count ?? 0); i++)
            {
                StackStep step = best!.Steps[i];
                bool ok = StackBenchmark.IsRight(step, expected[i]);
                right += ok ? 1 : 0;
                total++;
                Console.WriteLine($"    {(ok ? "✓" : "✗")} «{step.Need}» → {(step.Hit is null ? "ПРОБЕЛ" : step.Hit.Unit.Script ?? UnitReport.ShortId(step.Hit.Unit.Id))}" +
                    $"{(ok ? "" : $" (надо: {(expected[i].Length == 0 ? "пробел" : string.Join(" или ", expected[i]))})")}  [{step.Link}]");
            }

            if (answer.Check?.Passed == false)
                Console.WriteLine("    " + string.Join("\n    ", answer.Check.Details.Split('\n').TakeLast(3)));
        }

        Console.WriteLine($"Шагов выбрано верно: {right} из {total}. Проверку прошли {passed} из {StackBenchmark.Tasks.Count}, не прошли {failed}, без проверки {unchecked_}; пробелов {gaps}; {clock.Elapsed.TotalSeconds:F1} с");
        return 0;
    }
}
