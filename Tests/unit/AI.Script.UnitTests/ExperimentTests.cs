using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Опыты: план, повторы, журнал, сравнение с доказательством и тракт со сменными звеньями.
/// </summary>
/// <remarks>
/// Проверяется то, ради чего модуль и заведён: что повтор опыта даёт те же числа, что победитель
/// объявляется не по разнице в третьем знаке и что испытание можно найти в журнале и повторить.
/// </remarks>
public sealed class ExperimentTests
{
    private static RunOptions WithJournal(MemoryExperimentJournal journal) => new()
    {
        Seed = 7,
        Journal = journal,
    };

    // --- план ---

    [Fact]
    public void Grid_CoversEveryCombination()
    {
        RunResult result = Script.RunOk("""
            let план = exp.grid({ temp: [0.2, 0.7], model: ["a", "b", "c"] })

            emit точек = len(план)
            emit колонок = len(table.columns(план))
            emit первая = план[0].model
            """);

        Assert.Equal(6.0, result.Emitted["точек"]);
        Assert.Equal(2.0, result.Emitted["колонок"]);
        Assert.Equal("a", result.Emitted["первая"]);
    }

    [Fact]
    public void Grid_SingleValue_NeedsNoList()
    {
        Assert.Equal(2.0, Script.Number("len(exp.grid({ temp: [0.2, 0.7], model: \"a\" }))"));
    }

    [Fact]
    public void Grid_EmptyAxis_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = exp.grid({ temp: [] })");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    // --- повторы ---

    /// <summary>Повторы остаются строками: сравнение считается по ним, а не по одному числу.</summary>
    [Fact]
    public void Run_RepeatsEveryPoint()
    {
        RunResult result = Script.RunOk("""
            let план = exp.grid({ temp: [0.2, 0.7] })
            let итоги = план |> exp.run(p => { score: p.temp * 2 }, repeat: 3)

            emit строк = len(итоги)
            emit колонок = len(table.columns(итоги))
            emit повтор = итоги[2].repeat
            emit метрика = итоги[0].score
            """);

        Assert.Equal(6.0, result.Emitted["строк"]);
        Assert.Equal(3.0, result.Emitted["колонок"]);
        Assert.Equal(2.0, result.Emitted["повтор"]);
        Assert.Equal(0.4, (double)result.Emitted["метрика"]!, 9);
    }

    /// <summary>Одно число тоже метрика: ради «сколько попало в цель» запись писать незачем.</summary>
    [Fact]
    public void Run_PlainNumber_BecomesValueColumn()
    {
        Assert.Equal(4.0, Script.Number("(exp.grid({ n: [2] }) |> exp.run(p => p.n * 2))[0][\"value\"]"));
    }

    [Fact]
    public void Run_NonMetricOutcome_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = exp.grid({ n: [1] }) |> exp.run(p => \"готово\")");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
    }

    [Fact]
    public void Run_ZeroRepeats_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = exp.grid({ n: [1] }) |> exp.run(p => p.n, repeat: 0)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    /// <summary>
    /// Повтор всего опыта с тем же зерном даёт ту же таблицу — иначе сравнивать нечего.
    /// </summary>
    [Fact]
    public void Run_SameSeed_GivesSameOutcomes()
    {
        const string Source = """
            options { seed: 11, parallel: 4 }

            let план = exp.grid({ temp: [0.2, 0.7, 1.0] })
            let итоги = план |> exp.run(p => { score: math.random() }, repeat: 4, parallel: true)

            emit сумма = vec.sum(итоги["score"])
            """;

        double first = (double)Script.RunOk(Source).Emitted["сумма"]!;
        double again = (double)Script.RunOk(Source).Emitted["сумма"]!;

        Assert.Equal(first, again, 12);
    }

    // --- сравнение ---

    /// <summary>На данных без разницы значимость не объявляется — иначе она ничего не значит.</summary>
    [Fact]
    public void Compare_NoDifference_IsNotSignificant()
    {
        RunResult result = Script.RunOk("""
            options { seed: 3 }

            let план = exp.grid({ вариант: ["a", "b"] })
            let итоги = план |> exp.run(p => { score: signal.noise(1, sigma: 1)[0] }, repeat: 8)
            let вывод = итоги |> exp.compare(metric: "score", by: "вариант")

            emit доказано = вывод.significant
            emit сравнений = вывод.comparisons
            """);

        Assert.Equal(false, result.Emitted["доказано"]);
        Assert.Equal(1.0, result.Emitted["сравнений"]);
    }

    /// <summary>Разница в полторы единицы при разбросе в десятую доказывается уверенно.</summary>
    [Fact]
    public void Compare_RealDifference_IsProven()
    {
        RunResult result = Script.RunOk("""
            options { seed: 5 }

            let план = exp.grid({ вариант: ["слабый", "сильный"] })
            let итоги = план |> exp.run(
                p => { score: if p.вариант == "сильный" { 2.0 } else { 0.5 } + signal.noise(1, sigma: 0.1)[0] },
                repeat: 8)
            let вывод = итоги |> exp.compare(metric: "score", by: "вариант")

            emit лучший = вывод.best
            emit доказано = вывод.significant
            emit эффект = вывод.effect > 1
            emit групп = len(вывод.groups)
            """);

        Assert.Equal("сильный", result.Emitted["лучший"]);
        Assert.Equal(true, result.Emitted["доказано"]);
        Assert.Equal(true, result.Emitted["эффект"]);
        Assert.Equal(2.0, result.Emitted["групп"]);
    }

    [Fact]
    public void Compare_OneVariant_IsRejected()
    {
        Diagnostic error = Script.FailsWith("""
            let итоги = exp.grid({ вариант: ["a"] }) |> exp.run(p => { score: 1 }, repeat: 3)

            emit r = итоги |> exp.compare(metric: "score", by: "вариант")
            """);

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    /// <summary>Меньше — лучше там, где метрика это ошибка либо стоимость.</summary>
    [Fact]
    public void Compare_SmallerIsBetter_PicksSmallest()
    {
        RunResult result = Script.RunOk("""
            options { seed: 2 }

            let план = exp.grid({ вариант: ["дорогой", "дешёвый"] })
            let итоги = план |> exp.run(
                p => { цена: if p.вариант == "дешёвый" { 10.0 } else { 90.0 } + signal.noise(1, sigma: 1)[0] },
                repeat: 6)
            let вывод = итоги |> exp.compare(metric: "цена", by: "вариант", bigger: false)

            emit лучший = вывод.best
            emit доказано = вывод.significant
            """);

        Assert.Equal("дешёвый", result.Emitted["лучший"]);
        Assert.Equal(true, result.Emitted["доказано"]);
    }

    // --- журнал ---

    [Fact]
    public void Log_RecordsEveryTrial()
    {
        var journal = new MemoryExperimentJournal();

        RunResult result = Script.RunWith(Script.Host(), """
            let план = exp.grid({ temp: [0.2, 0.7] })
            let итоги = план |> exp.run(p => { score: p.temp }, repeat: 2)
            let журнал = exp.log()

            emit записей = len(журнал)
            emit колонки = table.columns(журнал)
            emit зерно = журнал[0].seed
            """, WithJournal(journal));

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(4.0, result.Emitted["записей"]);
        Assert.Equal(4, journal.Count);
        Assert.Equal(7.0, result.Emitted["зерно"]);
        Assert.Contains("score", ((System.Collections.IEnumerable)result.Emitted["колонки"]!).Cast<object?>());
    }

    /// <summary>Без журнала хоста скрипт не падает: <c>exp.log</c> отвечает пустой таблицей.</summary>
    [Fact]
    public void Log_WithoutJournal_IsEmpty()
    {
        Assert.Equal(0.0, Script.Number("len(exp.log())"));
    }

    /// <summary>Повтор испытания той же функцией обязан дать те же метрики.</summary>
    [Fact]
    public void Replay_RepeatsRecordedTrial()
    {
        RunResult result = Script.RunWith(Script.Host(), """
            let опыт = p => { score: p.temp * 10 }
            let итоги = exp.grid({ temp: [0.2, 0.7] }) |> exp.run(опыт)
            let повтор = exp.replay("1", опыт)

            emit совпало = повтор.same
            emit было = повтор.was.score
            """, WithJournal(new MemoryExperimentJournal()));

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(true, result.Emitted["совпало"]);
        Assert.Equal(7.0, (double)result.Emitted["было"]!, 9);
    }

    [Fact]
    public void Replay_UnknownId_SaysSo()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = exp.replay(\"41\", p => { score: 1 })",
            WithJournal(new MemoryExperimentJournal()));

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
    }

    // --- тракт ---

    [Fact]
    public void Variants_SwapOneLink()
    {
        RunResult result = Script.RunOk("""
            let мягкая = x => x + 1
            let жёсткая = x => x + 100
            let тракт = { очистка: мягкая, счёт: x => x * 2 }
            let варианты = exp.variants(тракт, swap: { очистка: [мягкая, жёсткая] })

            emit сколько = len(варианты)
            emit первый = exp.pipe(варианты[0], input: 1)
            emit второй = exp.pipe(варианты[1], input: 1)
            """);

        Assert.Equal(2.0, result.Emitted["сколько"]);
        Assert.Equal(4.0, result.Emitted["первый"]);
        Assert.Equal(202.0, result.Emitted["второй"]);
    }

    [Fact]
    public void Variants_UnknownLink_ListsLinks()
    {
        Diagnostic error = Script.FailsWith("""
            let тракт = { очистка: x => x }

            emit r = exp.variants(тракт, swap: { модель: [x => x] })
            """);

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("очистка", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Pipe_NonFunctionLink_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = exp.pipe({ звено: 5 }, input: 1)");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
    }

    // --- остановка по точности и бюджету ---

    /// <summary>Метрика без разброса: интервал нулевой, и опыт останавливается на третьем круге.</summary>
    [Fact]
    public void Until_NarrowInterval_StopsByPrecision()
    {
        RunResult result = Script.RunOk("""
            let итоги = exp.grid({ temp: [0.2, 0.7] })
                |> exp.run(p => { score: p.temp }, repeat: 30, until: { ci_width: 0.05 })

            emit строк = len(итоги)
            emit причина = итоги[0].stopped
            """);

        Assert.Equal(6.0, result.Emitted["строк"]);
        Assert.Equal("precision", result.Emitted["причина"]);
    }

    /// <summary>Шумная метрика узкого интервала не набирает: делаются все заказанные повторы.</summary>
    [Fact]
    public void Until_WideInterval_RunsAllRepeats()
    {
        RunResult result = Script.RunOk("""
            options { seed: 4 }

            let итоги = exp.grid({ temp: [0.2] })
                |> exp.run(p => { score: signal.noise(1, sigma: 5)[0] }, repeat: 6, until: { ci_width: 0.001, metric: "score" })

            emit строк = len(итоги)
            emit причина = итоги[0].stopped
            """);

        Assert.Equal(6.0, result.Emitted["строк"]);
        Assert.Equal("repeat", result.Emitted["причина"]);
    }

    [Fact]
    public void Until_UnknownCondition_ListsKnown()
    {
        Diagnostic error = Script.FailsWith("emit r = exp.grid({ n: [1] }) |> exp.run(p => p.n, until: { время: 5 })");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("ci_width", error.Hint, StringComparison.Ordinal);
    }

    // --- продолжение после срыва ---

    /// <summary>
    /// Опыт, сорвавшийся на середине, не пересчитывается заново: испытание-стадия берётся из
    /// кэша, и повторный запуск тратится только на то, что не успело посчитаться.
    /// </summary>
    /// <remarks>
    /// Кэш стадии считает по аргументам, поэтому повторы ОДНОЙ точки у детерминированного
    /// испытания тоже берутся из кэша. Там, где повтор обязан дать другое число (модель,
    /// случайность), испытание стадией не оформляют — иначе все повторы окажутся одинаковыми.
    /// </remarks>
    [Fact]
    public void Trial_AsCachedStage_IsReusedByNextRun()
    {
        var cache = new MemoryStageCache();

        const string Source = """
            @cache
            stage испытание(p: record) -> record { { score: p.temp * 2 } }

            let итоги = exp.grid({ temp: [0.2, 0.7] }) |> exp.run(p => испытание(p), repeat: 2)

            emit сумма = vec.sum(итоги["score"])
            """;

        RunResult first = Script.RunWith(Script.Host(), Source, new RunOptions { Cache = cache });
        RunResult again = Script.RunWith(Script.Host(), Source, new RunOptions { Cache = cache });

        Assert.True(first.Success, Script.Report(first));
        Assert.True(again.Success, Script.Report(again));
        Assert.Equal(first.Emitted["сумма"], again.Emitted["сумма"]);

        // В первом прогоне посчитаны две точки, а их вторые повторы взяты из кэша: у стадии с
        // теми же аргументами тот же ключ. Во втором прогоне из кэша приходит уже всё.
        Assert.Equal(2, first.Graph.CachedCount);
        Assert.Equal(4, again.Graph.CachedCount);
    }

    // --- правила данных ---

    [Fact]
    public void Validate_FindsDuplicatesAndGaps()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({ id: [1, 2, 2, 4], сумма: <10, 0, -5, 20> })
            let нарушения = t |> table.validate(rules: { id: "unique", сумма: "positive" })

            emit сколько = len(нарушения)
            emit правило = нарушения[0].rule
            emit строка = нарушения[0].row
            """);

        Assert.Equal(3.0, result.Emitted["сколько"]);
        Assert.Equal("unique", result.Emitted["правило"]);
        Assert.Equal(3.0, result.Emitted["строка"]);
    }

    /// <summary>Пропуск нарушает только <c>not_null</c>: иначе одна дырка даёт два нарушения.</summary>
    [Fact]
    public void Validate_MissingBreaksOnlyNotNull()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({ сумма: ["10", "н/д", "30"] }) |> table.clean()

            emit пропусков = len(t |> table.validate(rules: { сумма: "not_null" }))
            emit знака = len(t |> table.validate(rules: { сумма: "positive" }))
            """);

        Assert.Equal(1.0, result.Emitted["пропусков"]);
        Assert.Equal(0.0, result.Emitted["знака"]);
    }

    [Fact]
    public void Validate_SeveralRulesPerColumn()
    {
        Assert.Equal(2.0, Script.Number("""
            len(table.of({ x: <1, 1, -2> }) |> table.validate(rules: { x: ["unique", "positive"] }))
            """));
    }

    /// <summary>Своё правило — обычная лямбда: язык ради проверок не растёт.</summary>
    [Fact]
    public void Validate_PredicateRule()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({ доля: <0.2, 1.5, 0.9> })
            let нарушения = t |> table.validate(rules: { доля: v => v <= 1 })

            emit сколько = len(нарушения)
            emit правило = нарушения[0].rule
            """);

        Assert.Equal(1.0, result.Emitted["сколько"]);
        Assert.Equal("предикат", result.Emitted["правило"]);
    }

    [Fact]
    public void Validate_UnknownRule_ListsKnown()
    {
        Diagnostic error = Script.FailsWith("emit r = table.of({ x: <1> }) |> table.validate(rules: { x: \"чётное\" })");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("not_null", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_CleanTable_HasNoViolations()
    {
        Assert.Equal(0.0, Script.Number("""
            len(table.of({ id: [1, 2, 3], сумма: <10, 20, 30> })
                |> table.validate(rules: { id: "unique", сумма: ["not_null", "positive"] }))
            """));
    }

    // --- численный метод и сеть ---

    /// <summary>
    /// Лямбда численного метода не должна уходить в сеть: метод зовёт её тысячи раз, и каждое
    /// ожидание занимало бы поток. Отказ внятный, а не тихая блокировка.
    /// </summary>
    [Fact]
    public void NumericCallback_GoingToNetwork_IsRefused()
    {
        var options = new RunOptions { Sandbox = new SlowSandbox() };

        Diagnostic error = Script.FailsWith(
            "emit r = solve.root(x => x - len(io.files()), from: 0, to: 10)",
            options);

        Assert.Equal(DiagnosticCodes.FunctionFailed, error.Code);
        Assert.Contains("core.map", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Хранилище, которое и правда ждёт.
    /// </summary>
    /// <remarks>
    /// Хранилище в памяти отвечает мгновенно, и лямбда с чтением файла завершается синхронно —
    /// на нём разницу между «ждём» и «не ждём» не увидеть. Здесь чтение отдаёт управление, как
    /// это делает сеть либо настоящий диск.
    /// </remarks>
    private sealed class SlowSandbox : IScriptSandbox
    {
        public bool Enabled => true;

        public string Root => "медленное хранилище";

        public bool IsReadOnly => true;

        public async Task<IReadOnlyList<ScriptFileInfo>> ListAsync(
            string directory, string mask, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            return [];
        }

        public async Task<ScriptFileInfo?> InfoAsync(string path, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            return null;
        }

        public async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            return [];
        }

        public Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("хранилище только для чтения");
    }

    // --- найденное при проверке ---

    /// <summary>Параллельные круги опыта берут разные случайные числа: иначе повторы совпадают и точность ложная.</summary>
    [Fact]
    public void ParallelUntil_RoundsGetDifferentNoise()
    {
        RunResult result = Script.RunOk("""
            options { seed: 7 }
            let t = exp.grid({ v: [1, 2] })
                |> exp.run(p => { s: signal.noise(1, sigma: 5)[0] }, repeat: 10, parallel: true, until: { ci_width: 0.001 })
            emit rows = len(t)
            emit spread = stat.std(t["s"])
            """, new RunOptions { Parallelism = 4 });

        Assert.Equal(20.0, result.Emitted["rows"]);
        Assert.True((double)result.Emitted["spread"]! > 0.1);
    }

    [Fact]
    public void TwoParallelMaps_DrawDifferentNumbers()
    {
        RunResult result = Script.RunOk("""
            options { seed: 3 }
            let a = [1, 2, 3] |> core.map(x => signal.noise(1, sigma: 1)[0], parallel: true)
            let b = [1, 2, 3] |> core.map(x => signal.noise(1, sigma: 1)[0], parallel: true)
            emit same = a[0] == b[0]
            """, new RunOptions { Parallelism = 4 });

        Assert.Equal(false, result.Emitted["same"]);
    }

    [Fact]
    public void HugeRepeat_IsRejectedClearly()
    {
        Diagnostic error = Script.FailsWith("emit t = exp.grid({ v: [1, 2] }) |> exp.run(p => { s: 1 }, repeat: 30000000)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    /// <summary>Номер испытания больше наибольшего записанного: после обрезки журнала номера не повторяются.</summary>
    [Fact]
    public void JournalIds_ContinueAfterLargestId()
    {
        var journal = new MemoryExperimentJournal();

        journal.AppendAsync(new ExperimentEntry("41", "d", 0, new Dictionary<string, object?>(), new Dictionary<string, object?>(),
            DateTimeOffset.UtcNow, 0, 0, 0)).GetAwaiter().GetResult();

        Script.RunOk("emit t = exp.grid({ v: [1] }) |> exp.run(p => { s: 1 })", WithJournal(journal));

        Assert.Equal("42", journal.ReadAsync().GetAwaiter().GetResult()[^1].Id);
    }

    /// <summary>Два опыта пишут в один журнал одновременно: номера испытаний не совпадают.</summary>
    [Fact]
    public void ConcurrentRuns_GetDistinctJournalIds()
    {
        var journal = new MemoryExperimentJournal();

        Script.RunOk("""
            let runs = [1, 2, 3, 4] |> core.map(i => len(exp.grid({ v: [1, 2, 3] }) |> exp.run(p => { s: p.v })), parallel: true)
            emit total = vec.sum(vec.of(runs))
            """, new RunOptions { Journal = journal, Parallelism = 4 });

        var ids = journal.ReadAsync().GetAwaiter().GetResult().Select(entry => entry.Id).ToList();

        Assert.Equal(12, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
