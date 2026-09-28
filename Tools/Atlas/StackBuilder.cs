namespace AiFramework.Tools.Atlas;

/// <summary>Шаг стека: что нужно, чем это сделать и как шаг связан с предыдущим.</summary>
/// <param name="Need">Шаг задачи словами.</param>
/// <param name="Hit">Выбранная единица; <c>null</c> — пробел, в библиотеке этого нет.</param>
/// <param name="Link">Как данные приходят в шаг: «тип совпал», «через …», «нужен код-клей».</param>
/// <param name="Coverage">Доля слов шага, найденных в описании выбранной единицы.</param>
public sealed record StackStep(string Need, SearchHit? Hit, string Link, double Coverage);

/// <summary>Стек: шаги по порядку и итоговая оценка.</summary>
public sealed record StackPlan(IReadOnlyList<StackStep> Steps, double Score)
{
    /// <summary>Шаги, которые в библиотеке не нашлись.</summary>
    public IEnumerable<StackStep> Gaps => Steps.Where(step => step.Hit is null);
}

/// <summary>
/// Сборка стека: для каждого шага кандидаты из поиска, потом лучевой поиск цепочки.
/// </summary>
/// <remarks>
/// <para>
/// Цепочка оценивается по трём признакам: насколько кандидат подходит шагу (оценка поиска),
/// стыкуется ли выход предыдущего шага со входом этого (<see cref="TypeGraph"/>) и как часто
/// в реальном коде результат одного идёт во вход другого (потоки данных индекса).
/// </para>
/// <para>
/// Пробел объявляется, а не затыкается: если лучший кандидат покрывает меньше половины слов
/// шага и векторы не ставят его в первую тройку, шаг помечается как отсутствующий в
/// библиотеке. Натянутый метод опаснее пробела: его вставят в код, и ошибка всплывёт позже.
/// </para>
/// </remarks>
public sealed class StackBuilder(
    ApiSearch search, TypeGraph graph, IReadOnlyDictionary<((string, string), (string, string)), int> flows)
{
    private const int PerStep = 8;
    private const int Beam = 24;

    /// <summary>Порог покрытия по умолчанию: подобран на бенчмарке стеков.</summary>
    public const double DefaultCoverage = 0.45;

    /// <summary>Порог покрытия слов шага: ниже — шаг считается пробелом.</summary>
    public double MinCoverage { get; init; } = DefaultCoverage;

    /// <summary>Дополнять шаги переводами из глоссария.</summary>
    public bool Glossary { get; init; } = true;

    private const double RelevanceWeight = 1.0;
    private const double LinkWeight = 0.8;
    private const double FlowWeight = 0.3;

    /// <summary>Строит до <paramref name="plans"/> стеков по шагам задачи.</summary>
    public async Task<IReadOnlyList<StackPlan>> BuildAsync(IReadOnlyList<string> steps, int plans = 3, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var candidates = new List<(SearchHit Hit, double Coverage)>[steps.Count];

        for (int i = 0; i < steps.Count; i++)
        {
            candidates[i] = [.. (await CandidatesAsync(steps[i], cancellation))
                .Where(pair => pair.Coverage >= MinCoverage || pair.Hit.VectorRank <= 3)
                .Take(PerStep)];
        }

        List<(List<StackStep> Steps, double Score)> beams = [([], 0)];

        for (int i = 0; i < steps.Count; i++)
        {
            var next = new List<(List<StackStep>, double)>();

            foreach ((List<StackStep> chain, double score) in beams)
            {
                if (candidates[i].Count == 0)
                {
                    next.Add(([.. chain, new StackStep(steps[i], null, "в библиотеке не найдено", 0)], score));
                    continue;
                }

                foreach ((SearchHit hit, double coverage) in candidates[i])
                {
                    (string link, double fit) = Transition(chain, hit.Unit);
                    int flow = Previous(chain) is { } previous ? flows.GetValueOrDefault(((previous.Project, previous.Id), (hit.Unit.Project, hit.Unit.Id))) : 0;

                    next.Add(([.. chain, new StackStep(steps[i], hit, link, coverage)],
                        score + RelevanceWeight * hit.Score + LinkWeight * fit + FlowWeight * Math.Log(1 + flow)));
                }
            }

            beams = [.. next.OrderByDescending(beam => beam.Item2).Take(Beam)];
        }

        return [.. beams
            .DistinctBy(beam => string.Join('|', beam.Steps.Select(step => step.Hit?.Unit.Id ?? "—")))
            .Take(plans)
            .Select(beam => new StackPlan(beam.Steps, beam.Score))];
    }

    /// <summary>Кандидаты шага до отсечения по покрытию: для разбора, почему шаг стал пробелом.</summary>
    public async Task<IReadOnlyList<(SearchHit Hit, double Coverage)>> CandidatesAsync(string step, CancellationToken cancellation = default) =>
        [.. (await search.SearchAsync(step, new SearchOptions(Limit: 40, Glossary: Glossary, GlossaryWeight: 0.3), cancellation))
            .Where(hit => hit.Unit.Kind != "type")
            .Select(hit => (hit, search.Coverage(step, hit.Unit)))];

    /// <summary>
    /// Как данные доходят до кандидата: от выхода предыдущего шага, от входа стека (шаги над
    /// одними данными, как «среднее» и «медиана»), без входа вовсе или никак.
    /// </summary>
    private (string Link, double Fit) Transition(List<StackStep> chain, CodeUnit candidate)
    {
        IReadOnlyList<string> inputs = graph.DataInputs(candidate);
        CodeUnit? previous = Previous(chain);

        if (previous is null) return ($"начало: вход {(inputs.Count > 0 ? inputs[0] : "не нужен")}", 0.5);

        if (graph.DataOutput(previous) is { } output)
        {
            foreach (string input in inputs)
            {
                (int distance, CodeUnit? via) = graph.Link(output, input);
                if (distance == 0) return ($"тип совпал: {output}", 1.0);
                if (distance == 1) return ($"через {UnitReport.ShortId(via!.Id)}: {output} → {input}", 0.6);
            }
        }

        if (First(chain) is { } first && graph.DataInputs(first) is [var source, ..] && inputs.Any(input => graph.Link(source, input).Distance == 0))
            return ($"тот же вход: {source}", 0.5);

        if (inputs.Count == 0) return ("вход не нужен", 0.3);

        return ($"нужен код-клей: {graph.DataOutput(previous) ?? previous.Returns} → {inputs[0]}", 0);
    }

    private static CodeUnit? Previous(List<StackStep> chain) => chain.LastOrDefault(step => step.Hit != null)?.Hit!.Unit;

    private static CodeUnit? First(List<StackStep> chain) => chain.FirstOrDefault(step => step.Hit != null)?.Hit!.Unit;
}
