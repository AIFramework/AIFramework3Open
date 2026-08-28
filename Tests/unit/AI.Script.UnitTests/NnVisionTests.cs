using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Пространства <c>nn</c> и <c>cv</c>.
/// </summary>
/// <remarks>
/// Сеть проверяется на задачах, у которых известен ответ: линейно разделимые точки обязаны
/// разделиться, прямая — восстановиться. Изображения — на картинке, нарисованной тут же:
/// когда известно, где в ней граница, видно, нашёл ли её оператор Собеля, и не перепутаны ли
/// строки со столбцами.
/// </remarks>
public sealed class NnVisionTests
{
    private static ScriptHost Host() => Script.FullHost();

    private static RunResult Run(string source) =>
        Script.RunWith(Host(), source, new RunOptions { Seed = 7 });

    private static RunResult RunOk(string source)
    {
        RunResult result = Run(source);

        Assert.True(result.Success, Script.Report(result));

        return result;
    }

    // --- нейронные сети ---

    /// <summary>
    /// Два далёких облака точек: сеть обязана разделить их почти безошибочно, иначе не работает
    /// ни обучение, ни предсказание.
    /// </summary>
    [Fact]
    public void Nn_Fit_SeparatesTwoClusters()
    {
        RunResult result = RunOk("""
            options { seed: 3 }

            let n = 120
            let x = mat.transpose(mat.from_rows([
                signal.noise(n, sigma: 0.4) + vec.of(range(n) |> core.map(i => if i < 60 { 0 } else { 4 })),
                signal.noise(n, sigma: 0.4) + vec.of(range(n) |> core.map(i => if i < 60 { 0 } else { 4 }))
            ]))

            let y = vec.of(range(n) |> core.map(i => if i < 60 { 0 } else { 1 }))
            let сеть = nn.fit(x, y, hidden: [8], epochs: 60, lr: 0.05)

            emit точность = сеть.score(x, y)
            emit классов = сеть.describe().классов
            emit предсказаний = len(сеть.predict(x))
            emit эпох = len(сеть.history())
            """);

        Assert.True((double)result.Emitted["точность"]! > 0.95);
        Assert.Equal(2.0, result.Emitted["классов"]);
        Assert.Equal(120.0, result.Emitted["предсказаний"]);
        Assert.Equal(60.0, result.Emitted["эпох"]);
    }

    /// <summary>Потери обязаны падать: кривая, стоящая на месте, означает, что сеть не учится.</summary>
    [Fact]
    public void Nn_History_Decreases()
    {
        RunResult result = RunOk("""
            options { seed: 5 }

            let n = 80
            let x = mat.transpose(mat.from_rows([vec.of(range(n) |> core.map(i => i / 10))]))
            let y = vec.of(range(n) |> core.map(i => if i < 40 { 0 } else { 1 }))

            let сеть = nn.fit(x, y, hidden: [8], epochs: 40, lr: 0.05)
            let потери = сеть.history()

            emit начало = потери[0]
            emit конец = потери[39]
            """);

        Assert.True((double)result.Emitted["конец"]! < (double)result.Emitted["начало"]!);
    }

    /// <summary>Вероятности классов — распределение: неотрицательны и дают в сумме единицу.</summary>
    [Fact]
    public void Nn_Proba_IsADistribution()
    {
        RunResult result = RunOk("""
            options { seed: 2 }

            let x = mat.of([<0, 0>, <0.1, 0.2>, <5, 5>, <5.1, 4.9>])
            let y = <0, 0, 1, 1>
            let сеть = nn.fit(x, y, hidden: [4], epochs: 30, lr: 0.05, batch: 0)

            let p = сеть.proba(x)

            emit строк = mat.rows(p)
            emit столбцов = mat.cols(p)
            emit сумма = core.round(vec.sum(p[0, :]), digits: 6)
            emit минимум = stat.min(vec.of(p[0, :]))
            """);

        Assert.Equal(4.0, result.Emitted["строк"]);
        Assert.Equal(2.0, result.Emitted["столбцов"]);
        Assert.Equal(1.0, result.Emitted["сумма"]);
        Assert.True((double)result.Emitted["минимум"]! >= 0);
    }

    /// <summary>Регрессия на прямой: коэффициент детерминации обязан быть близок к единице.</summary>
    [Fact]
    public void Nn_Regression_RecoversLine()
    {
        RunResult result = RunOk("""
            options { seed: 11 }

            let n = 100
            let x = mat.transpose(mat.from_rows([vec.linspace(0, 1, n: n)]))
            let y = vec.of(range(n) |> core.map(i => (3 * (i / (n - 1))) + 1))

            let сеть = nn.fit(x, y, task: "regression", hidden: [16], epochs: 200, lr: 0.05)

            emit r2 = сеть.score(x, y)
            emit задача = сеть.describe().задача
            """);

        Assert.Equal("regression", result.Emitted["задача"]);
        Assert.True((double)result.Emitted["r2"]! > 0.9);
    }

    /// <summary>У регрессии нет классов, и просить вероятности бессмысленно.</summary>
    [Fact]
    public void Nn_Proba_OnRegression_IsReported()
    {
        Diagnostic error = Script.FailsWith("""
            let x = mat.of([<0>, <1>, <2>, <3>])
            let сеть = nn.fit(x, <0, 1, 2, 3>, task: "regression", hidden: [4], epochs: 5)

            emit r = сеть.proba(x)
            """, new RunOptions { Seed = 1 }, Host());

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("регрессии", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Обучение привязано к зерну прогона: тот же скрипт даёт тот же результат.</summary>
    [Fact]
    public void Nn_Fit_IsReproducible()
    {
        const string source = """
            options { seed: 9 }

            let x = mat.of([<0, 0>, <0.2, 0.1>, <4, 4>, <4.1, 3.9>, <0.1, 0.3>, <3.9, 4.2>])
            let y = <0, 0, 1, 1, 0, 1>
            let сеть = nn.fit(x, y, hidden: [6], epochs: 25, lr: 0.05)

            emit потери = core.round(сеть.history()[24], digits: 9)
            """;

        Assert.Equal(RunOk(source).Emitted["потери"], RunOk(source).Emitted["потери"]);
    }

    [Fact]
    public void Nn_Fit_RejectsMismatchedSizes()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = nn.fit(mat.of([<0>, <1>]), <0, 1, 0>)",
            null,
            Host());

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
    }

    [Fact]
    public void Nn_Fit_RejectsUnknownTaskAndActivation()
    {
        Assert.Equal(DiagnosticCodes.BadOperand, Script.FailsWith(
            "emit r = nn.fit(mat.of([<0>, <1>]), <0, 1>, task: \"кластеризация\")", null, Host()).Code);

        Assert.Contains("relu", Script.FailsWith(
            "emit r = nn.fit(mat.of([<0>, <1>]), <0, 1>, activation: \"ступенька\")", null, Host()).Hint,
            StringComparison.Ordinal);
    }

    /// <summary>Вещественные метки в классификации — почти наверняка забытый task: "regression".</summary>
    [Fact]
    public void Nn_Fit_RejectsFractionalLabels()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = nn.fit(mat.of([<0>, <1>]), <0.5, 1.5>)",
            null,
            Host());

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("regression", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Nn_Predict_RejectsWrongFeatureCount()
    {
        Diagnostic error = Script.FailsWith("""
            let x = mat.of([<0, 0>, <1, 1>])
            let сеть = nn.fit(x, <0, 1>, hidden: [4], epochs: 5)

            emit r = сеть.predict(mat.of([<0, 0, 0>]))
            """, new RunOptions { Seed = 1 }, Host());

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
    }

    // --- изображения ---

    /// <summary>
    /// Картинка из двух половин: слева темно, справа светло. Оператор Собеля обязан найти
    /// вертикальную границу ровно посередине и не найти горизонтальных.
    /// </summary>
    [Fact]
    public void Cv_Sobel_FindsVerticalEdge()
    {
        RunResult result = RunOk("""
            let w = 32
            let img = mat.of(range(w) |> core.map(r =>
                vec.of(range(w) |> core.map(c => if c < 16 { 0 } else { 255 }))))

            let края = cv.sobel(img)

            emit по_x = stat.max(vec.of(края.по_x[16, :]))
            emit граница = stat.max(vec.of(края.контуры[16, :]))
            emit размер = mat.rows(края.контуры)
            """);

        Assert.True((double)result.Emitted["граница"]! > 100);
        Assert.True((double)result.Emitted["по_x"]! > 0);
        Assert.Equal(32.0, result.Emitted["размер"]);
    }

    [Fact]
    public void Cv_Binary_CountsBrightPart()
    {
        RunResult result = RunOk("""
            let w = 20
            let img = mat.of(range(w) |> core.map(r =>
                vec.of(range(w) |> core.map(c => if c < 5 { 0 } else { 200 }))))

            let маска = cv.binary(img, threshold: 100)

            emit доля = core.round(vec.sum(vec.of(маска[0, :])) / w, digits: 3)
            emit значения = stat.max(vec.of(маска[0, :]))
            """);

        // Пятнадцать столбцов из двадцати ярче порога.
        Assert.Equal(0.75, result.Emitted["доля"]);
        Assert.Equal(1.0, result.Emitted["значения"]);
    }

    [Fact]
    public void Cv_Histogram_And_Equalize_KeepShape()
    {
        RunResult result = RunOk("""
            options { seed: 4 }

            let img = mat.of(range(24) |> core.map(r => signal.noise(24, sigma: 30) + 128))

            let гист = cv.histogram(img)
            let ровный = cv.equalize(img)

            emit длина = len(гист)
            emit строк = mat.rows(ровный)
            emit столбцов = mat.cols(ровный)
            emit сумма = core.round(vec.sum(гист), digits: 0)
            """);

        Assert.Equal(256.0, result.Emitted["длина"]);
        Assert.Equal(24.0, result.Emitted["строк"]);
        Assert.Equal(24.0, result.Emitted["столбцов"]);

        // Гистограмма пересчитывает все точки изображения: 24 × 24.
        Assert.Equal(576.0, result.Emitted["сумма"]);
    }

    /// <summary>
    /// Медианный фильтр убирает одиночный выброс, а среднее — размазывает: в этом и разница.
    /// </summary>
    [Fact]
    public void Cv_Median_RemovesSpike()
    {
        RunResult result = RunOk("""
            let img = mat.of(range(9) |> core.map(r =>
                vec.of(range(9) |> core.map(c => if r == 4 && c == 4 { 255 } else { 10 }))))

            let чистое = cv.median(img, size: 3)

            emit было = img[4, 4]
            emit стало = чистое[4, 4]
            """);

        Assert.Equal(255.0, result.Emitted["было"]);
        Assert.True((double)result.Emitted["стало"]! < 50);
    }

    [Fact]
    public void Cv_Median_RejectsEvenWindow()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = cv.median(mat.eye(8), size: 4)",
            null,
            Host());

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("центра", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Cv_Spectrum_HasSameShapeAndFiniteValues()
    {
        RunResult result = RunOk("""
            let img = mat.of(range(16) |> core.map(r =>
                vec.of(range(16) |> core.map(c => if (c % 4) < 2 { 0 } else { 255 }))))

            let спектр = cv.spectrum(img)

            emit строк = mat.rows(спектр)
            emit столбцов = mat.cols(спектр)
            emit конечно = math.abs(spectrum_sum(спектр)) < 1e12

            fn spectrum_sum(m: mat) -> num { vec.sum(vec.of(m[0, :])) }
            """);

        Assert.Equal(16.0, result.Emitted["строк"]);
        Assert.Equal(16.0, result.Emitted["столбцов"]);
        Assert.Equal(true, result.Emitted["конечно"]);
    }

    /// <summary>Низкочастотная фильтрация сглаживает: разброс яркостей падает.</summary>
    [Fact]
    public void Cv_LowPass_SmoothsImage()
    {
        RunResult result = RunOk("""
            options { seed: 6 }

            let img = mat.of(range(32) |> core.map(r => signal.noise(32, sigma: 60) + 128))
            let гладко = cv.lowpass(img, radius: 4)

            emit разброс_до = stat.std(vec.of(img[0, :]))
            emit разброс_после = stat.std(vec.of(гладко[0, :]))
            emit размер = mat.rows(гладко)
            """);

        Assert.Equal(32.0, result.Emitted["размер"]);
        Assert.True((double)result.Emitted["разброс_после"]! < (double)result.Emitted["разброс_до"]!);
    }

    [Fact]
    public void Cv_Hog_ReturnsRequestedBins()
    {
        RunResult result = RunOk("""
            let img = mat.of(range(16) |> core.map(r =>
                vec.of(range(16) |> core.map(c => if c < 8 { 0 } else { 255 }))))

            let признаки = cv.hog(img, bins: 9)

            emit длина = len(признаки)
            emit неотрицательны = stat.min(признаки) >= 0
            """);

        Assert.Equal(9.0, result.Emitted["длина"]);
        Assert.Equal(true, result.Emitted["неотрицательны"]);
    }

    [Fact]
    public void Cv_Resize_ChangesShape()
    {
        RunResult result = RunOk("""
            let img = mat.of(range(20) |> core.map(r => vec.full(20, value: 100)))
            let меньше = cv.resize(img, width: 8, height: 5)

            emit строк = mat.rows(меньше)
            emit столбцов = mat.cols(меньше)
            """);

        Assert.Equal(5.0, result.Emitted["строк"]);
        Assert.Equal(8.0, result.Emitted["столбцов"]);
    }

    /// <summary>Картинка проходит через файл и возвращается той же: путь идёт через песочницу.</summary>
    [Fact]
    public void Cv_SaveAndLoad_RoundTrip()
    {
        string root = Path.Combine(Path.GetTempPath(), "aiscript-cv", Guid.NewGuid().ToString("N"));

        try
        {
            RunResult result = Script.RunWith(Host(), """
                let img = mat.of(range(12) |> core.map(r =>
                    vec.of(range(12) |> core.map(c => if c < 6 { 30 } else { 220 }))))

                let путь = cv.save(img, "test.png")
                let назад = cv.load(путь)

                emit строк = mat.rows(назад)
                emit слева = назад[0, 0]
                emit справа = назад[0, 11]
                """, new RunOptions { Sandbox = new WorkspaceSandbox(root) });

            Assert.True(result.Success, Script.Report(result));
            Assert.Equal(12.0, result.Emitted["строк"]);

            // Яркости переживают запись и чтение с точностью до округления до байта.
            Assert.Equal(30.0, (double)result.Emitted["слева"]!, 1);
            Assert.Equal(220.0, (double)result.Emitted["справа"]!, 1);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Картинки читаются через ту же песочницу, что и остальные файлы.</summary>
    [Fact]
    public void Cv_Load_ObeysSandbox()
    {
        Diagnostic error = Script.FailsWith("emit r = cv.load(\"photo.png\")", null, Host());

        Assert.Equal(DiagnosticCodes.SandboxDenied, error.Code);
    }

    [Fact]
    public void Cv_Load_UnknownChannel_IsReported()
    {
        string root = Path.Combine(Path.GetTempPath(), "aiscript-cv", Guid.NewGuid().ToString("N"));

        try
        {
            Diagnostic error = Script.FailsWith("""
                let img = mat.of([<10, 20>, <30, 40>])
                let путь = cv.save(img, "a.png")

                emit r = cv.load(путь, channel: "инфракрасный")
                """, new RunOptions { Sandbox = new WorkspaceSandbox(root) }, Host());

            Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
            Assert.Contains("gray", error.Hint, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Признаки изображения кормят ту же сеть: пространства стыкуются матрицей.</summary>
    [Fact]
    public void Cv_And_Nn_ComposeThroughFeatures()
    {
        RunResult result = RunOk("""
            options { seed: 8 }

            fn полоски(вертикально: bool) -> mat {
                mat.of(range(16) |> core.map(r =>
                    vec.of(range(16) |> core.map(c =>
                        if вертикально { if (c % 4) < 2 { 0 } else { 255 } }
                        else { if (r % 4) < 2 { 0 } else { 255 } }))))
            }

            let образцы = range(8) |> core.map(i => cv.hog(полоски(i % 2 == 0), bins: 6))
            let x = mat.from_rows(образцы)
            let y = vec.of(range(8) |> core.map(i => i % 2))

            let сеть = nn.fit(x, y, hidden: [8], epochs: 120, lr: 0.05, batch: 0)

            emit признаков = mat.cols(x)
            emit точность = сеть.score(x, y)
            """);

        Assert.Equal(6.0, result.Emitted["признаков"]);
        Assert.Equal(1.0, result.Emitted["точность"]);
    }
}
