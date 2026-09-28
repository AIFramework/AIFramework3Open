using System.Text.RegularExpressions;

namespace AiFramework.Tools.Atlas;

/// <summary>Команды индекса: построить, показать единицу, сверить сборку из пакета.</summary>
internal static class IndexCommands
{
    public static async Task<int> IndexAsync(string folder, string? solution)
    {
        string root = GitRepository.TopLevel(folder);
        using var store = new UnitStore(AtlasSettings.IndexPath(root));

        SourceIndexer.Report report = await SourceIndexer.RunAsync(root, store, solution is null ? null : Path.GetFullPath(solution));

        Console.WriteLine($"""
            Решение: {report.Solution}, проектов: {report.Projects}
            Файлов: {report.Files}, разобрано заново: {report.ParsedFiles}, удалено: {report.RemovedFiles}
            Единиц: {report.Units}, изменилось по содержанию: {report.ChangedUnits}
            Вызовов: {report.Calls}
            Загрузка проектов: {report.Load.TotalSeconds:F1} с, разбор: {report.Scan.TotalSeconds:F1} с
            Индекс: {AtlasSettings.IndexPath(root)}
            """);

        // Сообщения MSBuild длинные и почти всегда про совместимость пакетов: хватает списка проектов.
        string[] troubled = [.. report.Failures
            .Select(failure => Regex.Match(failure, @"'([^']+\.csproj)'"))
            .Where(match => match.Success).Select(match => Path.GetFileNameWithoutExtension(match.Groups[1].Value)).Distinct()];

        if (report.Failures.Count > 0)
            Console.WriteLine($"Сообщений загрузки MSBuild: {report.Failures.Count}, проекты: {string.Join(", ", troubled)}");

        return 0;
    }

    /// <summary>
    /// Проект на библиотеке: связь, снимок библиотеки (переиндексация только при новом состоянии),
    /// индекс проекта и отчёт «проект ↔ библиотека».
    /// </summary>
    public static async Task<int> ProjectAsync(string folder, int limit, bool behavior)
    {
        string root = GitRepository.TopLevel(folder);

        if (LibraryLinks.Detect(root) is null)
        {
            Console.WriteLine($"{root} не подключает AIFramework: ни в одном .csproj нет ссылки на AI или AI.* вне репозитория и нет таких пакетов.");
            return 1;
        }

        await RefreshAsync(root);
        using AtlasContext context = AtlasContext.Open(root);
        Console.WriteLine();
        Console.WriteLine(await ProjectReport.BuildAsync(context, limit, behavior));
        return 0;
    }

    /// <summary>
    /// Аудит (цикл гигиены): по желанию сборка, обновление индексов (библиотеки — только если
    /// сменилось состояние git), дубли, исполнение из кэша и сверка с прошлым аудитом.
    /// </summary>
    public static async Task<int> AuditAsync(string folder, bool build)
    {
        string root = GitRepository.TopLevel(folder);

        if (build && !Build(root)) return 1;

        await RefreshAsync(root);
        using AtlasContext context = AtlasContext.Open(root);

        string report = await Audit.RunAsync(context, new Progress<int>(done => { if (done % 200 == 0) Console.Error.WriteLine($"пар: {done}"); }));
        string file = Path.Combine(context.CacheFolder, $"audit-{DateTime.Now:yyyyMMdd-HHmm}.md");
        File.WriteAllText(file, report);

        Console.WriteLine();
        Console.WriteLine(report);
        Console.WriteLine();
        Console.WriteLine($"Отчёт: {file}");
        return 0;
    }

    /// <summary>
    /// Обновляет индексы: у проекта — снимок библиотеки и свой индекс, у библиотеки — свой.
    /// Индекс, построенный по тому же состоянию git, не трогается.
    /// </summary>
    private static async Task RefreshAsync(string root)
    {
        if (LibraryLinks.Detect(root) is { } link)
        {
            Console.WriteLine("Связь с библиотекой: " + link.Describe());
            Console.WriteLine(await LibrarySnapshot.EnsureAsync(link));
        }

        using var store = new UnitStore(AtlasSettings.IndexPath(root));
        if (store.Count > 0 && store.Meta(UnitStore.StateKey) == GitRepository.State(root))
        {
            Console.WriteLine("Индекс актуален.");
            return;
        }

        SourceIndexer.Report report = await SourceIndexer.RunAsync(root, store);
        Console.WriteLine($"Индекс: разобрано файлов {report.ParsedFiles} из {report.Files}, единиц {report.Units}, {(report.Load + report.Scan).TotalSeconds:F0} с");
    }

    /// <summary>Сборка самого полного решения; ошибки печатаются, код выхода — успех.</summary>
    private static bool Build(string root)
    {
        string solution = Directory.GetFiles(root, "*.sln*").Where(path => Path.GetExtension(path) is ".sln" or ".slnx")
            .MaxBy(path => Regex.Count(File.ReadAllText(path), @"\.csproj")) ?? throw new InvalidOperationException($"В {root} нет решения.");

        Console.WriteLine($"Сборка {Path.GetFileName(solution)}…");
        var info = new System.Diagnostics.ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardInput = true, CreateNoWindow = true };
        foreach (string argument in new[] { "build", solution, "-nologo", "-v", "q" }) info.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(info)!;
        process.StandardInput.Close();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0) return true;
        Console.WriteLine(string.Join('\n', output.Split('\n').Where(line => line.Contains("error", StringComparison.OrdinalIgnoreCase)).Distinct().Take(20)));
        return false;
    }

    /// <summary>
    /// Переобучает калибровку вердикта на размеченных парах: решения журнала и итоги исполнения
    /// (из кэша, недостающие исполняются). Печатает точность на отложенной части.
    /// </summary>
    public static async Task<int> CalibrateAsync(string folder)
    {
        using AtlasContext context = AtlasContext.Open(folder);
        var examples = new Dictionary<string, CalibrationExample>(StringComparer.Ordinal);
        int fromLedger = 0;

        foreach (DupFinding finding in context.Dups.FindAll().Where(f => f.A.Kind == "method" && f.B.Kind == "method"))
        {
            string outcome = DupReport.Outcome(finding, await DupReport.RunAsync(context, finding.A, finding.B));
            bool? same = outcome switch
            {
                BehaviorProbe.Duplicate or BehaviorProbe.DuplicateExceptEdges or "дубль с поправкой" => true,
                DupReport.Divergence or "разные функции" or "связанные функции" => false,
                _ => null,
            };
            if (same is { } label) examples[Key(finding.A, finding.B)] = new CalibrationExample(Key(finding.A, finding.B), finding.Features, finding.Verdict, label);
        }

        context.BehaviorResults.Save();

        // Решение человека важнее исполнения той же пары.
        foreach (DupDecision decision in context.Ledger.Decisions.Where(d => d.HashA != "*" && d.Verdict is "дубль" or "перекрытие" or "нет связи"))
        {
            if (context.Find(decision.A[(decision.A.IndexOf('|') + 1)..]) is not { } a || context.Find(decision.B[(decision.B.IndexOf('|') + 1)..]) is not { } b) continue;
            if (context.Dups.Compare(a, b) is not { } finding) continue;

            examples[Key(a, b)] = new CalibrationExample(Key(a, b), finding.Features, finding.Verdict, decision.Verdict == "дубль");
            fromLedger++;
        }

        Calibration calibration = Calibration.Train([.. examples.Values]);
        calibration.Save(Calibration.DefaultPath);

        Console.WriteLine($"""
            Размеченных пар: {calibration.Examples} (из журнала {fromLedger}, из исполнения {calibration.Examples - fromLedger}), отложено {calibration.Holdout}
            Точность на отложенной части: правило «копия — дубль» {calibration.RuleAccuracy:P0}, ответ большинства {calibration.MajorityAccuracy:P0}, модель {calibration.ModelAccuracy:P0}
            Веса: свободный член {calibration.Weights[0]:F2}; {string.Join(", ", Calibration.Names.Select((name, i) => $"{name} {calibration.Weights[i + 1]:F2}"))}
            Файл: {Calibration.DefaultPath}
            """);
        return 0;
    }

    private static string Key(CodeUnit a, CodeUnit b) => string.CompareOrdinal(a.Id, b.Id) <= 0 ? a.Id + " ↔ " + b.Id : b.Id + " ↔ " + a.Id;

    public static int Show(string query, string folder)
    {
        using var store = new UnitStore(AtlasSettings.IndexPath(GitRepository.TopLevel(folder)));
        IReadOnlyList<CodeUnit> found = store.Find(query);

        if (found.Count == 0)
        {
            Console.WriteLine($"По «{query}» ничего не найдено. Индекс пуст? Запустите atlas index.");
            return 1;
        }

        Console.WriteLine(UnitReport.Describe(store, found[0]));

        if (found.Count > 1)
        {
            Console.WriteLine($"Ещё совпадения ({found.Count - 1}):");
            foreach (CodeUnit unit in found.Skip(1)) Console.WriteLine($"  {unit.Id}");
        }

        return 0;
    }

    /// <summary>
    /// Сравнение режима «только DLL» с индексом исходников той же сборки: состав открытого API
    /// должен совпасть, иначе индекс проекта на пакете и индекс библиотеки не сопоставить.
    /// </summary>
    public static int Package(string dll, string folder)
    {
        IReadOnlyList<CodeUnit> package = PackageReader.Read(dll);
        string assembly = Path.GetFileNameWithoutExtension(dll);

        using var store = new UnitStore(AtlasSettings.IndexPath(GitRepository.TopLevel(folder)));
        HashSet<string> source = [.. store.Units().Where(u => u.Project == assembly && u.Access == "public").Select(u => u.Id)];
        HashSet<string> fromPackage = [.. package.Select(u => u.Id)];

        Console.WriteLine($"""
            Сборка {assembly}: единиц из DLL {fromPackage.Count}, из исходников {source.Count}, общих {fromPackage.Intersect(source).Count()}
            С описанием в DLL: {package.Count(u => u.Doc.Length > 0)}
            """);

        foreach (string id in fromPackage.Except(source).Take(10)) Console.WriteLine($"  только в DLL: {id}");
        foreach (string id in source.Except(fromPackage).Take(10)) Console.WriteLine($"  только в исходниках: {id}");

        return fromPackage.SetEquals(source) ? 0 : 1;
    }
}
