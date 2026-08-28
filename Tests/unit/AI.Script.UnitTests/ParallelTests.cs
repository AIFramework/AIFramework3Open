using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Единственная форма параллелизма языка: <c>core.map(parallel: true)</c> и его соседи.
/// </summary>
/// <remarks>
/// Главное, что здесь проверяется, — не ускорение, а то, что параллельный прогон даёт ровно
/// тот же результат, что последовательный. Ускорение без этого свойства бесполезно: ответ,
/// зависящий от планировщика, нельзя ни проверить, ни воспроизвести.
/// </remarks>
public sealed class ParallelTests
{
    private static RunOptions Parallel(int degree = 4) => new() { Parallelism = degree, Seed = 42 };

    [Fact]
    public void Map_Parallel_KeepsOrder()
    {
        RunResult result = Script.RunOk("""
            let xs = range(200) |> core.map(i => i)
            let ys = xs |> core.map(x => x * x, parallel: true)

            emit first = ys[0]
            emit tenth = ys[10]
            emit last = ys[199]
            emit count = len(ys)
            """, Parallel());

        Assert.Equal(0.0, result.Emitted["first"]);
        Assert.Equal(100.0, result.Emitted["tenth"]);
        Assert.Equal(199.0 * 199.0, result.Emitted["last"]);
        Assert.Equal(200.0, result.Emitted["count"]);
    }

    /// <summary>Параллельный и последовательный прогоны обязаны совпасть до последнего числа.</summary>
    [Fact]
    public void Map_Parallel_MatchesSequential()
    {
        const string source = """
            let xs = range(300, from: 1) |> core.map(i => i)
            let ys = xs |> core.map(x => math.sqrt(x) * math.log(x + 1), parallel: PARALLEL)

            emit total = core.round(vec.sum(vec.of(ys)), digits: 9)
            """;

        object? sequential = Script.RunOk(source.Replace("PARALLEL", "false", StringComparison.Ordinal),
            Parallel()).Emitted["total"];

        object? parallel = Script.RunOk(source.Replace("PARALLEL", "true", StringComparison.Ordinal),
            Parallel()).Emitted["total"];

        Assert.Equal(sequential, parallel);
    }

    /// <summary>
    /// Случайные числа внутри параллельной лямбды воспроизводимы: каждая ветвь получает поток,
    /// выведенный из зерна прогона и номера элемента, а не общий на всех.
    /// </summary>
    /// <remarks>
    /// Сравнения двух прогонов здесь мало: оба идут в одном процессе, и невоспроизводимость
    /// «от запуска к запуску» такое сравнение пропускает — именно так и уцелело зерно ветви,
    /// построенное на <c>HashCode.Combine</c>, который засеян случайно при старте процесса.
    /// Поэтому число закреплено в тексте теста.
    /// </remarks>
    [Fact]
    public void Map_Parallel_RandomIsStableAcrossProcesses()
    {
        const string source = """
            options { seed: 7, parallel: 4 }

            let xs = range(50) |> core.map(i => i)
            let ys = xs |> core.map(x => math.random(low: 0, high: 1), parallel: true)

            emit first = core.round(ys[0], digits: 6)
            emit third = core.round(ys[2], digits: 6)
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(0.932414, result.Emitted["first"]);
        Assert.Equal(0.536385, result.Emitted["third"]);
    }

    /// <summary>Зерно ветви зависит только от зерна прогона и номера, а не от запуска.</summary>
    [Fact]
    public void BranchSeed_IsStable()
    {
        Assert.Equal(RunContext.BranchSeed(7, 0), RunContext.BranchSeed(7, 0));
        Assert.NotEqual(RunContext.BranchSeed(7, 0), RunContext.BranchSeed(7, 1));
        Assert.NotEqual(RunContext.BranchSeed(7, 0), RunContext.BranchSeed(8, 0));

        // Закреплённые числа: именно они ловят подмену перемешивания на хэш, засеваемый
        // случайно при старте процесса.
        Assert.Equal(1820458160, RunContext.BranchSeed(7, 0));
        Assert.Equal(1512814809, RunContext.BranchSeed(7, 1));
    }

    /// <summary>
    /// Библиотечная функция, берущая ГСЧ у прогона, в параллельной ветви обязана быть
    /// воспроизводимой так же, как <c>math.random</c>: иначе разбиение выборки внутри
    /// параллельного перебора давало бы разный ответ при каждом запуске.
    /// </summary>
    [Fact]
    public void Parallel_LibraryRandom_IsStable()
    {
        const string source = """
            options { seed: 5, parallel: 4 }

            let X = mat.transpose(mat.from_rows([signal.noise(200, sigma: 1), signal.noise(200, sigma: 1)]))
            let y = vec.of(range(200) |> core.map(i => i % 2))

            fn проба(k: num) -> num {
                let s = ml.split(X, y, test: 0.3)

                (s.y_test[0] * 100) + (s.y_test[1] * 10) + s.y_test[2]
            }

            let подписи = [1, 2, 3, 4, 5, 6, 7, 8] |> core.map(проба, parallel: true)

            emit подписи = str.join(подписи |> core.map(v => core.to_str(v)), by: ",")
            """;

        Assert.Equal(Script.RunOk(source).Emitted["подписи"], Script.RunOk(source).Emitted["подписи"]);
        Assert.Equal("111,0,100,111,100,11,1,110", Script.RunOk(source).Emitted["подписи"]);
    }

    [Fact]
    public void Filter_Parallel_KeepsOrderAndSelection()
    {
        RunResult result = Script.RunOk("""
            let xs = range(100) |> core.map(i => i)
            let even = xs |> core.filter(x => x % 2 == 0, parallel: true)

            emit count = len(even)
            emit first = even[0]
            emit second = even[1]
            """, Parallel());

        Assert.Equal(50.0, result.Emitted["count"]);
        Assert.Equal(0.0, result.Emitted["first"]);
        Assert.Equal(2.0, result.Emitted["second"]);
    }

    [Fact]
    public void FlatMap_ConcatenatesLists()
    {
        RunResult result = Script.RunOk("""
            let words = ["раз два", "три четыре пять"]
            let all = words |> core.flat_map(w => str.split(w, by: " "))

            emit count = len(all)
            emit first = all[0]
            emit last = all[4]
            """);

        Assert.Equal(5.0, result.Emitted["count"]);
        Assert.Equal("раз", result.Emitted["first"]);
        Assert.Equal("пять", result.Emitted["last"]);
    }

    [Fact]
    public void FlatMap_RejectsNonListResult()
    {
        Diagnostic error = Script.FailsWith("emit r = [1, 2] |> core.flat_map(x => x)");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
        Assert.Contains("core.map", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>Запись во внешнее имя из параллельной лямбды — гонка, и она ловится до запуска.</summary>
    [Fact]
    public void Parallel_WritingToOuterName_IsRejectedBeforeRun()
    {
        Diagnostic error = Script.CheckFailsWith("""
            let total = 0
            let xs = [1, 2, 3]

            emit r = len(xs |> core.map(x => { set total = total + x
                                               x }, parallel: true))
            """);

        Assert.Equal(DiagnosticCodes.UnboundSet, error.Code);
        Assert.Contains("гонка", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>Своё имя внутри лямбды писать можно: оно принадлежит ветви.</summary>
    [Fact]
    public void Parallel_WritingToOwnName_IsAllowed()
    {
        RunResult result = Script.RunOk("""
            let xs = [1, 2, 3]

            let ys = xs |> core.map(x => {
                let acc = 0

                for i in 0..x { set acc = acc + i }

                acc
            }, parallel: true)

            emit last = ys[2]
            """, Parallel());

        Assert.Equal(3.0, result.Emitted["last"]);
    }

    /// <summary>Последовательному вызову присваивание внешнему имени по-прежнему разрешено.</summary>
    [Fact]
    public void Sequential_WritingToOuterName_IsAllowed()
    {
        RunResult result = Script.RunOk("""
            let total = 0
            let xs = [1, 2, 3]

            let ignored = xs |> core.map(x => { set total = total + x
                                          x })

            emit total = total
            """);

        Assert.Equal(6.0, result.Emitted["total"]);
    }

    /// <summary>Отказ в одной ветви останавливает весь параллельный участок.</summary>
    [Fact]
    public void Parallel_FailureInBranch_FailsRun()
    {
        Diagnostic error = Script.FailsWith("""
            let xs = range(40) |> core.map(i => i)

            emit r = len(xs |> core.map(x => { assert x < 20, "слишком большое ${x}"
                                               x }, parallel: true))
            """, Parallel());

        Assert.Equal(DiagnosticCodes.AssertionFailed, error.Code);
    }

    [Fact]
    public void Parallel_RespectsStepLimit()
    {
        RunResult result = Script.Run("""
            let xs = range(64) |> core.map(i => i)

            emit r = len(xs |> core.map(x => {
                let total = 0

                while true { set total = total + 1 }

                total
            }, parallel: true))
            """, new RunOptions { Parallelism = 4, Limits = new ScriptLimits { Steps = 20000 } });

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.StepLimit, result.Error!.Code);
    }

    /// <summary>Опция прогона задаёт число ветвей; аргумент отвечает лишь «можно ли здесь».</summary>
    [Fact]
    public void Options_Parallel_IsAccepted()
    {
        CheckResult check = Script.Check("options { parallel: 4 }\nemit r = 1");

        Assert.True(check.Success, check.Render());
        Assert.Empty(check.Diagnostics);
    }
}
