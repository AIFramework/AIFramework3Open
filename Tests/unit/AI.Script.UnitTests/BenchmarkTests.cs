using AI.Script.Hosting;
using AI.Script.Llm;
using AI.Script.Std;

namespace AI.Script.UnitTests;

/// <summary>
/// Эталонный набор задач LLM-контура.
/// </summary>
/// <remarks>
/// Признак готовности этапа — «модель решает 8 из 10» — проверяется живой моделью и потому
/// здесь недостижим: тест обязан быть быстрым, бесплатным и воспроизводимым. Здесь проверяется
/// то, что от живой модели не зависит и без чего критерий бессмыслен: что каждая задача
/// набора вообще решается средствами языка, и что проверяющая часть считает решения верно.
/// <para>
/// Иначе провал живого прогона нельзя было бы истолковать: то ли модель не справилась, то ли
/// задача не имеет решения в этой библиотеке, то ли проверка ошибается.
/// </para>
/// </remarks>
public sealed class BenchmarkTests
{
    /// <summary>
    /// Эталонные решения — по одному на задачу, в порядке набора.
    /// </summary>
    /// <remarks>
    /// Написаны так, как их написала бы модель по карточке языка: без хитростей и без
    /// подгонки под проверку.
    /// </remarks>
    private static readonly string[] Solutions =
    [
        // статистика
        """
        let xs = <12, 7, 3, 21, 15, 9, 4>

        emit среднее = stat.mean(xs)
        emit медиана = stat.median(xs)
        """,

        // фильтр и сумма
        """
        let xs = [4, -2, 15, 0, -7, 23, 8]
        let положительные = xs |> core.filter(x => x > 0)

        emit сумма = vec.sum(vec.of(положительные))
        emit сколько = len(положительные)
        """,

        // таблица и группировка
        """
        let продажи = table.of({
            город: ["Москва", "Тверь", "Москва", "Тверь", "Казань"],
            сумма: <120, 30, 80, 45, 60>
        })

        let по_городам = продажи |> table.group_by("город", agg: {
            выручка: rows => vec.sum(rows["сумма"])
        })

        let москва = по_городам |> table.filter(row => row.город == "Москва")

        emit москва = москва[0].выручка
        emit городов = len(по_городам)
        """,

        // корреляция
        """
        emit корреляция = stat.corr(<1, 2, 3, 4, 5>, <2, 4, 6, 8, 10>)
        """,

        // матрица
        """
        let a = mat.of([<1, 2>, <3, 4>])
        let b = mat.of([<5, 6>, <7, 8>])
        let c = a * b

        emit элемент = c[0, 0]
        """,

        // кластеризация
        """
        options { seed: 1 }

        let точки = mat.of([<0, 0>, <0.2, 0.1>, <0.1, 0.2>, <5, 5>, <5.2, 4.9>, <4.9, 5.1>])
        let модель = ml.kmeans(точки, k: 2)
        let метки = модель.predict(точки)

        emit групп = stat.max(метки) - stat.min(метки) + 1
        emit объектов = mat.rows(точки)
        """,

        // текст
        """
        let текст = "Мама мыла раму, а рама мыла маму"

        emit слов = len(str.split(текст, by: " "))
        """,

        // сигнал
        """
        let fs = 1000
        let t = signal.time(1, fs: fs)
        let s = signal.sine(t, freq: 50)
        let спектр = dsp.fft(s, fs: fs)

        emit частота = спектр.freq[vec.argmax(спектр.amp)]
        """,

        // регрессия
        """
        let x = <1, 2, 3, 4, 5>
        let y = <3, 5, 7, 9, 11>
        let модель = ml.linreg(x, y)

        emit предсказание = модель.predict(<6>)[0]
        """,

        // производная
        """
        emit производная = solve.derivative_fn(x => (x * x * x) + (2 * x), at: 2)
        """,
    ];

    private static ScriptHost Host() => StandardLibrary.CreateHost();

    /// <summary>
    /// Каждая задача набора решается языком, и проверяющая часть засчитывает решение.
    /// </summary>
    [Fact]
    public async Task Benchmark_ReferenceSolutions_SolveEveryTask()
    {
        var llm = new FakeLlm(Solutions);
        var writer = new ScriptWriter(llm, Host(), new ScriptWriterOptions
        {
            RunOptions = static () => RunProfiles.Trusted(),
        });

        BenchmarkReport report = await ScriptBenchmark.RunAsync(writer);

        Assert.Equal(ScriptBenchmark.Tasks.Count, report.Total);
        Assert.Equal(report.Total, report.Solved);
        Assert.Equal(Solutions.Length, report.Total);
    }

    /// <summary>
    /// Проверка обязана отличать верное решение от правдоподобного: скрипт, отдающий
    /// не то число, засчитан быть не может.
    /// </summary>
    [Fact]
    public async Task Benchmark_WrongAnswer_IsNotCounted()
    {
        var llm = new FakeLlm("emit среднее = 100\nemit медиана = 100");
        var writer = new ScriptWriter(llm, Host(), new ScriptWriterOptions
        {
            MaxRepairs = 0,
            RunOptions = static () => RunProfiles.Trusted(),
        });

        BenchmarkReport report = await ScriptBenchmark.RunAsync(writer, [ScriptBenchmark.Tasks[0]]);

        Assert.Equal(0, report.Solved);
        Assert.Contains("ожидалось", report.Outcomes[0].Problem!, StringComparison.Ordinal);
    }

    /// <summary>Отсутствующий результат отличается от неверного, и отчёт это показывает.</summary>
    [Fact]
    public async Task Benchmark_MissingEmit_IsReported()
    {
        var llm = new FakeLlm("let x = 1", "let x = 2", "let x = 3");
        var writer = new ScriptWriter(llm, Host(), new ScriptWriterOptions
        {
            RunOptions = static () => RunProfiles.Trusted(),
        });

        BenchmarkReport report = await ScriptBenchmark.RunAsync(writer, [ScriptBenchmark.Tasks[0]]);

        Assert.Equal(0, report.Solved);
        Assert.Contains("нет результата", report.Outcomes[0].Problem!, StringComparison.Ordinal);
    }

    [Fact]
    public void Benchmark_Report_ShowsScoreAndTaskNames()
    {
        var report = new BenchmarkReport(
        [
            new(ScriptBenchmark.Tasks[0], new ScriptSolution("x", null, [], 1), null),
            new(ScriptBenchmark.Tasks[1], new ScriptSolution("y", null, [], 3), "нет результата 'сумма'"),
        ]);

        string text = report.Render();

        Assert.Contains("Решено 1 из 2", text, StringComparison.Ordinal);
        Assert.Contains("+ статистика", text, StringComparison.Ordinal);
        Assert.Contains("− фильтр и сумма, попыток: 3", text, StringComparison.Ordinal);
    }

    /// <summary>Задачи сформулированы словами задачи, а не языка: имён функций в них нет.</summary>
    [Fact]
    public void Benchmark_Tasks_DoNotLeakLanguageNames()
    {
        string[] forbidden = ["emit ", "stat.", "vec.", "mat.", "table.", "|>"];

        foreach (BenchmarkTask task in ScriptBenchmark.Tasks)
        {
            foreach (string name in forbidden)
            {
                Assert.False(
                    task.Task.Contains(name, StringComparison.Ordinal),
                    $"задача «{task.Name}» подсказывает решение: {name}");
            }
        }
    }
}
