using System.Text;
using System.Text.RegularExpressions;

namespace AiFramework.Tools.DocsLint;

/// <summary>
/// Линтер документации теории (Docs/Tutorials), канон — Docs/Tutorials/STRUCTURE.md.
///
/// Две категории проблем:
///   • ОШИБКИ — объективно сломанное (битая ссылка TheoryFile, нет H1, файл-заглушка).
///     Роняют сборку всегда, в baseline не заносятся.
///   • ПРЕДУПРЕЖДЕНИЯ — незаполненные разделы канона. Их в существующих
///     документах сотни, поэтому действует baseline: уже известные записаны в
///     .docslint-baseline и не роняют CI, а любое НОВОЕ предупреждение — роняет.
///     Так канон применяется к новым документам, не требуя переписать 200 старых.
///
/// Запуск:
///   dotnet run --project Tools/DocsLint                      только ошибки
///   dotnet run --project Tools/DocsLint -- --strict          + новые предупреждения (режим CI)
///   dotnet run --project Tools/DocsLint -- --fix             автозамена синонимов разделов
///   dotnet run --project Tools/DocsLint -- --update-baseline перезаписать baseline
///
/// В GitHub Actions сообщения печатаются workflow-аннотациями и видны в диффе PR.
/// </summary>
internal static class Program
{
    private const int MinChars = 800;
    private const string BaselineName = ".docslint-baseline";

    /// <summary>Разделы канона. Отсутствие — предупреждение (см. baseline).</summary>
    private static readonly string[] CanonSections =
        ["Постановка задачи", "API", "Код", "Сложность", "Ограничения"];

    /// <summary>
    /// ПЕРЕИМЕНОВАНИЯ: разные написания одного и того же раздела.
    /// Заголовок правится в файле (--fix), содержимое не меняется —
    /// поэтому сюда попадают только строгие синонимы.
    /// </summary>
    private static readonly Dictionary<string, string> Renames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["API (C#)"]              = "API",
        ["API C#"]                = "API",
        ["API и использование"]   = "API",
        ["Применение"]            = "Применения",
        ["Пример"]                = "Примеры",
        ["Пример использования"]  = "Примеры",
        ["Примеры использования"] = "Примеры",
        ["Идея алгоритма"]        = "Теория",
    };

    /// <summary>
    /// ЭКВИВАЛЕНТЫ: раздел канона -> заголовки, которые его закрывают по смыслу.
    /// Файл НЕ правится: «Достоинства и ограничения» содержит и достоинства
    /// тоже, переименование в «Ограничения» сделало бы заголовок ложью.
    /// </summary>
    private static readonly Dictionary<string, string[]> Equivalents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Постановка задачи"] = ["Назначение", "Обзор", "Определение", "Задача"],
        ["Теория"]            = ["Математическая основа", "Алгоритм", "Формулы"],
        ["Ограничения"]       = ["Достоинства и ограничения", "Замечания", "Числовые замечания"],
        ["Код"]               = ["Примеры", "Быстрый старт", "Пример кода"],
    };

    /// <summary>Не теория алгоритма — проверяется только на канон разделов не проверяется.</summary>
    private static readonly HashSet<string> NonTheoryFiles =
        new(StringComparer.OrdinalIgnoreCase) { "README.md", "STRUCTURE.md" };

    private static readonly List<Issue> Issues = [];
    private static string _root = "";

    private static int Main(string[] args)
    {
        bool fix            = args.Contains("--fix");
        bool strict         = args.Contains("--strict");
        bool updateBaseline = args.Contains("--update-baseline");

        _root = GetArg(args, "--root") ?? FindRepoRoot();
        string docsRoot = Path.Combine(_root, "Docs", "Tutorials");
        if (!Directory.Exists(docsRoot))
        {
            Console.Error.WriteLine("Не найден Docs/Tutorials. Укажите --root <путь к репозиторию>.");
            return 2;
        }

        string modulesDir = Path.Combine(_root, "Demo", "WebUI", "AiFrameworkDemo", "AiFrameworkDemo", "Modules");
        var referenced = ScanModules(modulesDir, docsRoot);

        int fixedCount = 0;
        foreach (string file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories).Order())
            fixedCount += CheckFile(file, referenced, fix);

        Console.WriteLine($"Документов: {Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories).Count()}, " +
                          $"связано с демо: {referenced.Count}.");

        string baselinePath = Path.Combine(docsRoot, BaselineName);
        if (updateBaseline)
        {
            WriteBaseline(baselinePath);
            return 0;
        }

        return Summarize(baselinePath, fix, fixedCount, strict);
    }

    // ── Разбор модулей демо ────────────────────────────────────────────

    private sealed record Reference(string Module, string TutorialFolder, string MdFile);

    /// <summary>
    /// Сопоставляет модулям их TutorialFolder и упомянутые .md.
    /// Регулярки вместо Roslyn осознанно: ради двух строковых литералов
    /// тянуть компилятор в утилиту не стоит.
    /// </summary>
    private static Dictionary<string, Reference> ScanModules(string modulesDir, string docsRoot)
    {
        var result = new Dictionary<string, Reference>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(modulesDir))
        {
            Add(Severity.Error, "MD010", modulesDir, 0,
                "Каталог модулей демо не найден — ссылки TheoryFile не проверены.");
            return result;
        }

        var reFolder = new Regex(@"TutorialFolder\s*(?:=>|=)\s*""([^""]+)""", RegexOptions.Compiled);
        var reMd     = new Regex(@"""([\w\-.]+\.md)""", RegexOptions.Compiled);

        foreach (string moduleDir in Directory.EnumerateDirectories(modulesDir).Order())
        {
            string[] csFiles = Directory.GetFiles(moduleDir, "*.cs", SearchOption.AllDirectories);

            string? folder = null;
            foreach (string cs in csFiles)
            {
                Match m = reFolder.Match(File.ReadAllText(cs));
                if (m.Success) { folder = m.Groups[1].Value; break; }
            }

            if (folder is null) continue;   // модуль без теории — например SolversMath

            foreach (string cs in csFiles)
            {
                string text = File.ReadAllText(cs);
                foreach (Match m in reMd.Matches(text))
                {
                    string md   = m.Groups[1].Value;
                    string path = Path.Combine(docsRoot, folder, md);

                    if (!File.Exists(path))
                    {
                        Add(Severity.Error, "MD010", cs, LineOf(text, m.Index),
                            $"TheoryFile «{md}» не найден: ожидается Docs/Tutorials/{folder}/{md}. " +
                            "На странице алгоритма читатель увидит заглушку.");
                        continue;
                    }

                    result[Norm(path)] = new Reference(Path.GetFileName(moduleDir), folder, md);
                }
            }
        }

        return result;
    }

    // ── Проверка одного документа ──────────────────────────────────────

    /// <returns>Число автоисправлений, применённых к файлу.</returns>
    private static int CheckFile(string file, Dictionary<string, Reference> referenced, bool fix)
    {
        string name = Path.GetFileName(file);
        string text = File.ReadAllText(file);

        // Сохраняем исходный стиль переводов строк: иначе --fix переписал бы
        // файл целиком и дифф стал бы нечитаемым.
        string eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        text = text.Replace("\r\n", "\n");
        string[] lines = text.Split('\n');

        if (NonTheoryFiles.Contains(name)) return 0;

        bool isLinked = referenced.ContainsKey(Norm(file));

        // Строки внутри ``` — не заголовки: «# Конвертация в ONNX» в bash-блоке
        // и «# comment» в python-блоке иначе считались бы вторым H1.
        bool[] inCode = MarkFencedLines(lines);

        // ── Синонимы разделов: единственная автоисправимая категория ──
        int fixedHere = 0;
        var rebuilt = new StringBuilder();
        bool changed = false;

        for (int i = 0; i < lines.Length; i++)
        {
            Match h = inCode[i] ? Match.Empty : Regex.Match(lines[i], @"^(#{2,6})\s+(.+?)\s*$");
            if (h.Success && Renames.TryGetValue(h.Groups[2].Value.Trim(), out string? canonical))
            {
                if (fix)
                {
                    lines[i] = $"{h.Groups[1].Value} {canonical}";
                    changed = true;
                    fixedHere++;
                }
                else
                {
                    Add(Severity.Error, "MD005", file, i + 1,
                        $"Неканоническое название раздела «{h.Groups[2].Value.Trim()}» — должно быть «{canonical}». " +
                        "Исправляется автоматически: dotnet run --project Tools/DocsLint -- --fix");
                }
            }
            rebuilt.Append(lines[i]).Append('\n');
        }

        if (changed)
        {
            string outText = rebuilt.ToString();
            if (!text.EndsWith('\n') && outText.EndsWith('\n')) outText = outText[..^1];
            File.WriteAllText(file, outText.Replace("\n", eol));
            text   = outText;
            lines  = outText.Split('\n');
            inCode = MarkFencedLines(lines);
        }

        // ── H1: ровно один и первым заголовком ──
        var h1Lines = new List<int>();
        int firstHeading = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (inCode[i]) continue;
            if (Regex.IsMatch(lines[i], @"^#\s+\S")) h1Lines.Add(i + 1);
            if (firstHeading < 0 && Regex.IsMatch(lines[i], @"^#{1,6}\s+\S")) firstHeading = i + 1;
        }

        if (h1Lines.Count == 0)
            Add(Severity.Error, "MD001", file, 1,
                "Нет заголовка «# Название» — он используется как заголовок страницы теории.");
        else if (h1Lines.Count > 1)
            Add(Severity.Error, "MD002", file, h1Lines[1],
                $"Заголовок «# » должен быть один, найдено {h1Lines.Count}. Понизьте лишние до «## ».");
        else if (h1Lines[0] != firstHeading)
            Add(Severity.Error, "MD003", file, firstHeading, "Первым заголовком файла должен быть «# Название».");

        // ── Объём: короткий файл — заглушка, а не теория ──
        if (text.Length < MinChars)
            Add(Severity.Error, "MD004", file, 1,
                $"Слишком короткий документ: {text.Length} символов при минимуме {MinChars}.");

        // ── Разделы канона ──
        var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < lines.Length; i++)
        {
            if (inCode[i]) continue;
            Match h = Regex.Match(lines[i], @"^#{2,6}\s+(.+?)\s*$");
            if (h.Success) sections.Add(h.Groups[1].Value.Trim());
        }

        foreach (string s in CanonSections)
        {
            if (sections.Contains(s)) continue;

            // Раздел может быть назван по-другому, но закрывать ту же роль
            if (Equivalents.TryGetValue(s, out string[]? alt) && alt.Any(sections.Contains)) continue;

            Add(Severity.Warning, "MD006", file, 1, $"Нет раздела канона «## {s}».");
        }

        if (!text.Contains("```csharp", StringComparison.Ordinal) &&
            !text.Contains("```cs", StringComparison.Ordinal))
            Add(Severity.Warning, "MD007", file, 1,
                "Нет ни одного блока ```csharp — читателю нечего скопировать в свой код.");

        if (!isLinked)
            Add(Severity.Warning, "MD008", file, 1,
                "Файл не связан ни с одним AlgoDef: в демо он недоступен. Добавьте демо или удалите документ.");

        return fixedHere;
    }

    // ── Baseline ───────────────────────────────────────────────────────

    /// <summary>Ключ записи baseline: правило + путь. Без номера строки — иначе
    /// любая правка выше по файлу «оживляла» бы старое предупреждение.</summary>
    private static string Key(Issue i) => $"{i.Code} {Relative(i.File)}";

    private static HashSet<string> ReadBaseline(string path)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return set;

        foreach (string line in File.ReadAllLines(path))
        {
            string s = line.Trim();
            if (s.Length == 0 || s.StartsWith('#')) continue;
            set.Add(s);
        }
        return set;
    }

    private static void WriteBaseline(string path)
    {
        var keys = Issues.Where(i => i.Level == Severity.Warning)
                         .Select(Key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Baseline линтера документации (Tools/DocsLint).");
        sb.AppendLine("# Здесь зафиксированы ИЗВЕСТНЫЕ незаполненные разделы канона в старых документах:");
        sb.AppendLine("# они не роняют CI, но новые нарушения — роняют.");
        sb.AppendLine("# Дописав раздел, удалите соответствующую строку (или прогоните --update-baseline).");
        sb.AppendLine("# Формат: <код правила> <путь относительно корня репозитория>");
        sb.AppendLine();
        foreach (string k in keys) sb.AppendLine(k);

        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"Baseline обновлён: {keys.Count} записей -> {Relative(path)}");
    }

    // ── Отчёт ──────────────────────────────────────────────────────────

    private enum Severity { Error, Warning }

    private sealed record Issue(Severity Level, string Code, string File, int Line, string Message);

    private static void Add(Severity level, string code, string file, int line, string message) =>
        Issues.Add(new Issue(level, code, file, line, message));

    private static int Summarize(string baselinePath, bool fix, int fixedCount, bool strict)
    {
        bool ci = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
        var baseline = ReadBaseline(baselinePath);

        var errors = Issues.Where(i => i.Level == Severity.Error).ToList();
        var warnAll = Issues.Where(i => i.Level == Severity.Warning).ToList();
        var warnNew = warnAll.Where(i => !baseline.Contains(Key(i))).ToList();

        foreach (Issue i in errors.Concat(warnNew)
                     .OrderBy(i => i.Level).ThenBy(i => i.File, StringComparer.Ordinal).ThenBy(i => i.Line))
        {
            string rel = Relative(i.File);
            if (ci)
            {
                string kind = i.Level == Severity.Error ? "error" : "warning";
                Console.WriteLine($"::{kind} file={rel},line={i.Line},title={i.Code}::{i.Message}");
            }
            else
            {
                string tag = i.Level == Severity.Error ? "ОШИБКА " : "предупр.";
                Console.WriteLine($"{tag} [{i.Code}] {rel}:{i.Line}  {i.Message}");
            }
        }

        if (fix) Console.WriteLine($"Автоисправлений применено: {fixedCount}.");

        int baselined = warnAll.Count - warnNew.Count;
        Console.WriteLine($"Итого: ошибок {errors.Count}, новых предупреждений {warnNew.Count}, " +
                          $"в baseline {baselined}.");

        if (errors.Count > 0) return 1;
        if (strict && warnNew.Count > 0) return 1;
        return 0;
    }

    // ── Утилиты ────────────────────────────────────────────────────────

    private static string Relative(string path) =>
        Path.GetRelativePath(_root, path).Replace('\\', '/');

    private static string Norm(string p) => Path.GetFullPath(p).Replace('\\', '/');

    /// <summary>
    /// Отмечает строки внутри ограждённых блоков кode (``` или ~~~), включая
    /// сами строки-ограждения. Нужно, чтобы «# comment» в bash/python-блоке
    /// не принимался за заголовок Markdown.
    /// </summary>
    private static bool[] MarkFencedLines(string[] lines)
    {
        var inCode = new bool[lines.Length];
        string? fence = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();

            if (fence is null)
            {
                Match open = Regex.Match(trimmed, @"^(`{3,}|~{3,})");
                if (open.Success)
                {
                    fence = open.Groups[1].Value[..1];   // символ ограждения
                    inCode[i] = true;
                    continue;
                }
            }
            else
            {
                inCode[i] = true;
                if (Regex.IsMatch(trimmed, $@"^{Regex.Escape(fence)}{{3,}}\s*$")) fence = null;
            }
        }

        return inCode;
    }

    private static int LineOf(string text, int index) => text[..index].Count(c => c == '\n') + 1;

    private static string? GetArg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "Docs", "Tutorials"))) return dir;
            DirectoryInfo? parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
