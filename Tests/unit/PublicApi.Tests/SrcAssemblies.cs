using System.Xml.Linq;

namespace AIFramework.PublicApi.Tests;

/// <summary>
/// Сборки каталога src и файлы их снимков.
/// </summary>
internal static class SrcAssemblies
{
    /// <summary>
    /// Сборки src, для которых снимок не проверяется: имя сборки и причина.
    /// Других исключений нет: каждая сборка src обязана иметь снимок.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Excluded = new Dictionary<string, string>
    {
#if AI_LIGHT_BUILD
        ["AI.NeuralNetworks.Gpu"] =
            "лёгкая сборка не собирает GPU-библиотеку (build/Light.props); её снимок проверяют полная сборка и CI",
#endif
    };

    /// <summary>
    /// Корень репозитория — ближайший сверху каталог с AIFramework.sln
    /// (тот же признак использует Tests/Directory.Build.props).
    /// </summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    /// Каталог снимков в исходниках (не в выходе сборки): снимки лежат в git рядом с тестом.
    /// </summary>
    public static string SnapshotDirectory { get; } =
        Path.Combine(RepositoryRoot, "Tests", "unit", "PublicApi.Tests", "Snapshots");

    /// <summary>
    /// Имена сборок всех проектов src/*/*.csproj по алфавиту. Тот же шаблон стоит
    /// в ProjectReference файла PublicApi.Tests.csproj.
    /// </summary>
    public static IReadOnlyList<string> All()
    {
        var names = new List<string>();

        foreach (string directory in Directory.EnumerateDirectories(Path.Combine(RepositoryRoot, "src")))
        {
            foreach (string project in Directory.EnumerateFiles(directory, "*.csproj"))
                names.Add(AssemblyNameOf(project));
        }

        string? duplicate = names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (duplicate != null)
            throw new InvalidOperationException($"Имя сборки {duplicate} встречается в src несколько раз.");

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Сборки, у которых проверяется снимок: все сборки src, кроме <see cref="Excluded"/>.
    /// </summary>
    public static IEnumerable<string> Checked() => All().Where(name => !Excluded.ContainsKey(name));

    /// <summary>
    /// Путь к файлу снимка сборки.
    /// </summary>
    public static string SnapshotPath(string assemblyName) => Path.Combine(SnapshotDirectory, assemblyName + ".txt");

    /// <summary>
    /// Имена сборок, для которых лежат файлы снимков.
    /// </summary>
    public static IEnumerable<string> SnapshotNames() =>
        Directory.Exists(SnapshotDirectory)
            ? Directory.EnumerateFiles(SnapshotDirectory, "*.txt").Select(path => Path.GetFileNameWithoutExtension(path)!)
            : [];

    private static string AssemblyNameOf(string projectPath)
    {
        // AssemblyName задаётся прямо в csproj (например, AI.SignalLabs собирается в AI.SignalLab),
        // иначе MSBuild берёт имя файла проекта
        string? declared = XDocument.Load(projectPath)
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "AssemblyName")
            ?.Value.Trim();

        if (string.IsNullOrEmpty(declared))
            return Path.GetFileNameWithoutExtension(projectPath);

        if (declared.Contains("$(", StringComparison.Ordinal))
            throw new InvalidOperationException($"{projectPath}: AssemblyName через свойство MSBuild ({declared}) тест не вычисляет.");

        return declared;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AIFramework.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException($"Не найден корень репозитория (AIFramework.sln) выше {AppContext.BaseDirectory}.");
    }
}
