using AI.Script.Hosting;

namespace AI.Script.UnitTests;

/// <summary>
/// Приёмка этапа: пример из <c>examples/00_m0_tour.ais</c> должен исполняться без правок.
/// </summary>
/// <remarks>
/// Пример лежит в репозитории, а не в строке теста, намеренно: он одновременно документация
/// и приёмочный тест, и разойтись им нельзя.
/// </remarks>
public sealed class ExamplesTests
{
    private static readonly ScriptHost ExampleHost = Script.FullHost();

    private static string ExamplePath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "AI.Script", "examples")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        return Path.Combine(directory!.FullName, "src", "AI.Script", "examples", name);
    }

    /// <summary>
    /// Любой пример в <c>examples/</c> обязан проходить проверку: список файлов не перечисляется
    /// в тесте, иначе добавленный пример останется непроверенным ровно до того дня, когда на нём
    /// кто-нибудь споткнётся. Заведомо неисполнимые примеры замысла лежат в <c>examples/planned/</c>
    /// и сюда не попадают.
    /// </summary>
    [Fact]
    public void AllAcceptanceExamples_CheckClean()
    {
        string[] files = Directory.GetFiles(Path.GetDirectoryName(ExamplePath("x"))!, "*.ais");

        Assert.NotEmpty(files);

        foreach (string file in files)
        {
            CheckResult result = ExampleHost.Check(File.ReadAllText(file), Path.GetFileName(file));

            Assert.True(result.Success, result.Render());
        }
    }

    [Fact]
    public void Example_M0Tour_ChecksClean()
    {
        string source = File.ReadAllText(ExamplePath("00_m0_tour.ais"));
        CheckResult result = ExampleHost.Check(source, "00_m0_tour.ais");

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Example_M0Tour_Runs()
    {
        string source = File.ReadAllText(ExamplePath("00_m0_tour.ais"));
        RunResult result = Script.Run(source, new RunOptions { FileName = "00_m0_tour.ais" });

        Assert.True(result.Success, Script.Report(result));

        Assert.Equal(7.0, result.Emitted["размер"]);
        Assert.Equal(15.143, result.Emitted["среднее"]);
        Assert.Single(result.Artifacts);
        Assert.Contains(result.Transcript, line => line.Contains("разброс", StringComparison.Ordinal));
    }

    [Fact]
    public void Example_M1Pipeline_ChecksClean()
    {
        string source = File.ReadAllText(ExamplePath("01_m1_pipeline.ais"));
        CheckResult result = ExampleHost.Check(source, "01_m1_pipeline.ais");

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Example_M1Pipeline_Runs()
    {
        string root = Path.Combine(Path.GetTempPath(), "aiscript-m1", Guid.NewGuid().ToString("N"));

        try
        {
            string source = File.ReadAllText(ExamplePath("01_m1_pipeline.ais"));

            RunResult result = Script.Run(source, new RunOptions
            {
                FileName = "01_m1_pipeline.ais",
                Sandbox = new WorkspaceSandbox(root),
            });

            Assert.True(result.Success, Script.Report(result));

            // Выручка считается по 'net': возвраты клиента b обнуляют его вклад, поэтому
            // лидером оказывается a, а не тот, у кого больше сумма заказов.
            Assert.Equal(3.0, result.Emitted["клиентов"]);
            Assert.Equal("a", result.Emitted["лидер"]);
            Assert.Equal(480.0, result.Emitted["выручка"]);
            Assert.Equal(4.0, result.Emitted["признаков"]);
            Assert.Equal(4.0, result.Emitted["обучающая"]);
            Assert.Equal(2.0, result.Emitted["тестовая"]);
            Assert.Equal(3.0, result.Emitted["месяцы"]);
            Assert.Single(result.Artifacts);

            Assert.True(File.Exists(Path.Combine(root, "m1", "sales.csv")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("02_m3_ml.ais")]
    [InlineData("03_m3_dsp.ais")]
    [InlineData("04_m3_control.ais")]
    public void Example_M3_ChecksClean(string name)
    {
        string source = File.ReadAllText(ExamplePath(name));
        CheckResult result = ExampleHost.Check(source, name);

        Assert.True(result.Success, result.Render());
    }

    [Fact]
    public void Example_M3Ml_Runs()
    {
        string source = File.ReadAllText(ExamplePath("02_m3_ml.ais"));
        RunResult result = Script.Run(source, new RunOptions { FileName = "02_m3_ml.ais" });

        Assert.True(result.Success, Script.Report(result));

        Assert.True((double)result.Emitted["точность"]! > 0.8);
        Assert.True((double)result.Emitted["силуэт"]! > 0.4);
        Assert.Equal(1.0, result.Emitted["компонент"]);

        // Три показа: сводка таблицей, матрица ошибок и диаграмма рассеяния.
        Assert.Equal(3, result.Artifacts.Count);
        Assert.Equal("table", result.Artifacts[0].Kind);
        Assert.Equal("plot", result.Artifacts[1].Kind);
        Assert.Equal("plot", result.Artifacts[2].Kind);
    }

    [Fact]
    public void Example_M3Dsp_Runs()
    {
        string source = File.ReadAllText(ExamplePath("03_m3_dsp.ais"));
        RunResult result = Script.Run(source, new RunOptions { FileName = "03_m3_dsp.ais" });

        Assert.True(result.Success, Script.Report(result));

        Assert.Equal(8000.0, result.Emitted["длина"]);
        Assert.InRange((double)result.Emitted["основной_тон"]!, 420.0, 460.0);
        Assert.True((double)result.Emitted["снр_после"]! > (double)result.Emitted["снр_до"]!);
        Assert.True((double)result.Emitted["подавление_раз"]! > 10);

        Assert.Single(result.Artifacts);
        Assert.Equal("plot", result.Artifacts[0].Kind);
    }

    [Fact]
    public void Example_M3Control_Runs()
    {
        string source = File.ReadAllText(ExamplePath("04_m3_control.ais"));
        RunResult result = Script.Run(source, new RunOptions { FileName = "04_m3_control.ais" });

        Assert.True(result.Success, Script.Report(result));

        Assert.Equal(2.0, result.Emitted["порядок"]);
        Assert.True((double)result.Emitted["ошибка_модели"]! < 0.01);
        Assert.True((double)result.Emitted["kp"]! > 0);

        // Смысл примера: разомкнутый объект застревает на своём коэффициенте передачи,
        // а замкнутый контур доводит выход до задания.
        Assert.True((double)result.Emitted["без_регулятора"]! < 0.5);
        Assert.InRange((double)result.Emitted["установившееся"]!, 0.95, 1.05);
        Assert.True((double)result.Emitted["перерегулирование"]! < 0.5);

        Assert.Single(result.Artifacts);
        Assert.Equal("plot", result.Artifacts[0].Kind);
    }

    /// <summary>
    /// Приёмка этапа M5: поиск по корпусу работает без сети и без ключей, а обращение к
    /// модели остаётся необязательным.
    /// </summary>
    /// <remarks>
    /// Пример намеренно исполняется на хосте без служб: так проверяется, что офлайн-часть
    /// контура — индекс, выдача, сборка контекста — не зависит ни от чего внешнего. Сам
    /// запрос к модели тестом не покрывается: он требует чужой службы и денег.
    /// </remarks>
    [Fact]
    public void Example_M5Rag_RunsWithoutNetwork()
    {
        string source = File.ReadAllText(ExamplePath("06_m5_rag.ais"));

        RunResult result = Script.RunWith(ExampleHost, source, new RunOptions { FileName = "06_m5_rag.ais" });

        Assert.True(result.Success, Script.Report(result));

        Assert.Equal("words", result.Emitted["вид_индекса"]);
        Assert.Equal(6.0, result.Emitted["документов"]);
        Assert.Equal(3.0, result.Emitted["найдено_строк"]);
        Assert.Equal(0.0, result.Emitted["лучший"]);
        Assert.Equal("модель этому прогону недоступна", result.Emitted["ответ"]);
        Assert.Equal(0.0, result.Emitted["вызовов_к_модели"]);
        Assert.True((double)result.Emitted["длина_контекста"]! > 0);

        Assert.Single(result.Artifacts);
        Assert.Equal("table", result.Artifacts[0].Kind);
    }

    /// <summary>
    /// Приёмка этапа M4: конвейер из стадий считается один раз, а повторный прогон того же
    /// скрипта с тем же кэшем не выполняет ни одной стадии заново.
    /// </summary>
    [Fact]
    public void Example_M4Stages_RunsAndReusesCache()
    {
        string source = File.ReadAllText(ExamplePath("05_m4_stages.ais"));
        var cache = new MemoryStageCache();

        RunResult first = Script.Run(source, Options(cache));
        RunResult second = Script.Run(source, Options(cache));

        Assert.True(first.Success, Script.Report(first));
        Assert.True(second.Success, Script.Report(second));

        Assert.Equal(4, first.Stats.Stages);
        Assert.Equal(4, second.Stats.Stages);

        // Первый прогон считает три разные стадии, четвёртый узел — повторный вызов
        // 'признаки' с тем же аргументом; второй прогон не считает ничего.
        Assert.Equal(1, first.Stats.CachedStages);
        Assert.Equal(4, second.Stats.CachedStages);
        Assert.True(second.Stats.Steps < first.Stats.Steps / 10);

        Assert.Equal(first.Emitted["точность"], second.Emitted["точность"]);
        Assert.Equal(true, second.Emitted["признаки_совпали"]);
        Assert.Equal(8.0, second.Emitted["проверено_k"]);
        Assert.Single(second.Artifacts);

        static RunOptions Options(MemoryStageCache cache) => new()
        {
            FileName = "05_m4_stages.ais",
            Cache = cache,
            Parallelism = 4,
        };
    }

    [Fact]
    public void Example_M3_AreDeterministic()
    {
        foreach (string name in new[] { "02_m3_ml.ais", "03_m3_dsp.ais", "04_m3_control.ais", "05_m4_stages.ais" })
        {
            string source = File.ReadAllText(ExamplePath(name));

            RunResult first = Script.Run(source);
            RunResult second = Script.Run(source);

            Assert.True(first.Success, Script.Report(first));
            Assert.Equal(first.Emitted, second.Emitted);
        }
    }

    [Fact]
    public void Example_M0Tour_IsDeterministic()
    {
        string source = File.ReadAllText(ExamplePath("00_m0_tour.ais"));

        RunResult first = Script.Run(source);
        RunResult second = Script.Run(source);

        Assert.Equal(first.Transcript, second.Transcript);
        Assert.Equal(first.Emitted["медиана"], second.Emitted["медиана"]);
    }
}
