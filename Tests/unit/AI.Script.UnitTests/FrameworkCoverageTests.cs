using System.Reflection;
using System.Text.RegularExpressions;

namespace AI.Script.UnitTests;

/// <summary>
/// Полнота привязки: каждая сборка фреймворка либо доступна языку, либо названо, почему нет.
/// </summary>
/// <remarks>
/// Утверждение «язык покрывает фреймворк» проверяется здесь, а не в README: иначе новая сборка
/// выпадает молча. Так уже было — десять сборок появились после начала работы над языком, и
/// ни одна проверка этого не заметила.
/// <para>
/// Привязанной считается сборка, на которую ссылаются метаданные сборок скрипта. Компилятор
/// записывает туда только те ссылки, чьи типы код действительно использует, поэтому строка в
/// <c>.csproj</c> без единой функции проверку не пройдёт, а транзитивная зависимость — тем
/// более: AI.Econometrics приезжала в скрипт вместе с AI.Economics, не давая языку ничего.
/// </para>
/// </remarks>
public sealed class FrameworkCoverageTests
{
    /// <summary>
    /// Сборки, которые язык не подключает сознательно, — с причиной.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Excluded = new Dictionary<string, string>
    {
        ["AI.Charts"] = "основа графиков; язык строит их через AI.Charts.JS — пространство plot",
        ["AI.Charts.Avalonia"] = "окно Avalonia — интерфейс, а не расчёт",
        ["AI.Charts.WinForms"] = "окно WinForms — интерфейс, а не расчёт",
        ["AI.ImageEditor"] = "интерактивный редактор с сессией правок; обработка изображений — пространство cv",
        ["AI.ONNX"] = "исполнение чужого файла модели требует своего решения о песочнице (DESIGN §18.41)",
        ["AI.NeuralNetworks.Onnx"] = "то же, что AI.ONNX (DESIGN §18.41)",
        ["AI.NeuralNetworks.Gpu"] = "сети, которые обучает язык, малы для видеокарты (DESIGN §18.42)",
        ["AI.Faiss"] = "на размерах прототипа поиск перебором точен и быстр (DESIGN §18.28)",
    };

    /// <summary>
    /// Сборки, которые подключить нужно, но пока не подключены: долг, а не решение.
    /// </summary>
    /// <remarks>
    /// Список нужен, чтобы долг был виден, а не чтобы его узаконить: привязанную сборку отсюда
    /// обязательно убрать — это проверяет <see cref="Lists_DoNotMentionBoundOrMissingAssemblies"/>.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> Pending = new Dictionary<string, string>
    {
        ["AI.Physics"] = "физика: механика, термодинамика, оптика, акустика",
        ["AI.Earth"] = "науки о Земле: геодезия, проекции, астрономия",
        ["AI.Biology"] = "биология: последовательности, филогения, популяции",
        ["AI.Psychology"] = "психология: психометрия, психофизика, решения",
        ["AI.DataPrepaire"] = "не разобрана: пересекается с prep, nlp и io, нужен аудит",
    };

    [Fact]
    public void EveryFrameworkAssembly_IsBoundOrListed()
    {
        HashSet<string> bound = BoundAssemblies();

        string[] silent = [.. FrameworkAssemblies()
            .Where(name => !bound.Contains(name) && !Excluded.ContainsKey(name) && !Pending.ContainsKey(name))];

        Assert.True(silent.Length == 0,
            $"не привязаны и не объяснены: {string.Join(", ", silent)}. " +
            "Привяжите сборку либо внесите её в Excluded с причиной или в Pending");
    }

    /// <summary>
    /// Списки не должны врать: привязанная или удалённая сборка в них — устаревшая запись.
    /// </summary>
    [Fact]
    public void Lists_DoNotMentionBoundOrMissingAssemblies()
    {
        HashSet<string> bound = BoundAssemblies();
        HashSet<string> present = [.. FrameworkAssemblies()];

        foreach ((string name, string reason) in Excluded.Concat(Pending))
        {
            Assert.True(present.Contains(name), $"сборки {name} нет в src — уберите её из списка");
            Assert.False(bound.Contains(name), $"сборка {name} уже привязана — уберите её из списка");
            Assert.False(string.IsNullOrWhiteSpace(reason), $"у {name} не указана причина");
        }
    }

    [Fact]
    public void Lists_DoNotOverlap() =>
        Assert.Empty(Excluded.Keys.Intersect(Pending.Keys));

    // --- внутреннее ---

    /// <summary>Сборки, на которые ссылаются метаданные сборок скрипта.</summary>
    private static HashSet<string> BoundAssemblies()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (string script in Projects(scripts: true))
        {
            foreach (AssemblyName reference in Assembly.Load(script).GetReferencedAssemblies())
            {
                if (reference.Name != null) names.Add(reference.Name);
            }
        }

        return names;
    }

    private static IEnumerable<string> FrameworkAssemblies() => Projects(scripts: false);

    /// <summary>
    /// Имена сборок проектов из <c>src</c>: сборки скрипта либо все остальные.
    /// </summary>
    /// <remarks>
    /// Имя сборки берётся из <c>AssemblyName</c> проекта, если он его задаёт: имя папки с ним
    /// совпадает не всегда, а сравнивать приходится с метаданными.
    /// </remarks>
    private static IEnumerable<string> Projects(bool scripts)
    {
        foreach (string directory in Directory.GetDirectories(SourceRoot(), "AI*"))
        {
            string folder = Path.GetFileName(directory);
            string project = Path.Combine(directory, folder + ".csproj");

            if (!File.Exists(project)) continue;

            bool isScript = folder == "AI.Script" || folder.StartsWith("AI.Script.", StringComparison.Ordinal);

            if (isScript != scripts) continue;

            Match declared = Regex.Match(File.ReadAllText(project), "<AssemblyName>([^<]+)</AssemblyName>");

            yield return declared.Success ? declared.Groups[1].Value.Trim() : folder;
        }
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "AI.Script")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        return Path.Combine(directory!.FullName, "src");
    }
}
