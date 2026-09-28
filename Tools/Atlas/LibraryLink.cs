using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AiFramework.Tools.Atlas;

/// <summary>Как проект подключает AIFramework.</summary>
/// <param name="Sources">Исходниками (<c>ProjectReference</c>) или пакетами (<c>PackageReference</c>).</param>
/// <param name="Root">Исходники: корень репозитория библиотеки. Пакеты: папка снимка в кэше.</param>
/// <param name="Version">Исходники: состояние git (<see cref="GitRepository.State"/>). Пакеты: версия.</param>
/// <param name="Assemblies">Сборки библиотеки, на которые ссылается проект: имя → DLL (у исходников пусто).</param>
/// <param name="Projects">Проекты, где найдена ссылка.</param>
public sealed record LibraryLink(bool Sources, string Root, string Version, IReadOnlyDictionary<string, string> Assemblies, IReadOnlyList<string> Projects)
{
    /// <summary>Способ подключения словами.</summary>
    public string Describe() => Sources
        ? $"исходники {Root}, {GitRepository.Describe(Version)}; ссылка в {string.Join(", ", Projects)}"
        : $"пакеты {string.Join(", ", Assemblies.Keys)} {Version}; ссылка в {string.Join(", ", Projects)}";
}

/// <summary>
/// Распознаёт по <c>.csproj</c> проекта, подключает ли он AIFramework: ссылкой на проект
/// <c>AI</c> или <c>AI.*</c> вне своего репозитория или пакетом с таким же именем.
/// </summary>
/// <remarks>
/// Ссылка часто задана через свойство (<c>$(AiFrameworkCore)</c> в KnowledgeGraph), поэтому
/// свойства из <c>Directory.Build.props</c> и самого проекта подставляются, как это сделал бы
/// MSBuild; условия не вычисляются, вместо них проверяется, что файл по ссылке существует.
/// Полная оценка MSBuild здесь избыточна: нужна одна ссылка, а не сборка проекта.
/// </remarks>
public static class LibraryLinks
{
    private static readonly Regex Property = new(@"\$\((?<name>[A-Za-z_][\w.]*)\)", RegexOptions.Compiled);

    /// <summary>Ссылка на библиотеку; <c>null</c> — проект её не подключает.</summary>
    /// <param name="projectRoot">Корень репозитория проекта.</param>
    /// <param name="packages">Папка пакетов NuGet; <c>null</c> — <c>NUGET_PACKAGES</c> или <c>~/.nuget/packages</c>.</param>
    public static LibraryLink? Detect(string projectRoot, string? packages = null)
    {
        projectRoot = Path.GetFullPath(projectRoot);
        packages ??= Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        string? libraryRoot = null;
        var projects = new SortedSet<string>(StringComparer.Ordinal);
        var assemblies = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var versions = new SortedSet<string>(StringComparer.Ordinal);

        foreach (string csproj in Directory.EnumerateFiles(projectRoot, "*.csproj", SearchOption.AllDirectories).Where(path => !Generated(projectRoot, path)))
        {
            (XDocument document, Dictionary<string, string> properties) = Evaluate(csproj, projectRoot);
            string name = Path.GetFileNameWithoutExtension(csproj);

            foreach (XElement reference in document.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
            {
                string target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csproj)!, Expand((string?)reference.Attribute("Include") ?? "", properties, csproj)));
                if (!File.Exists(target) || Inside(projectRoot, target) || !IsLibrary(Path.GetFileNameWithoutExtension(target))) continue;

                libraryRoot ??= GitRepository.TopLevel(Path.GetDirectoryName(target)!);
                projects.Add(name);
            }

            foreach (XElement reference in document.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
            {
                string id = (string?)reference.Attribute("Include") ?? "";
                string version = Expand((string?)reference.Attribute("Version") ?? reference.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value ?? "", properties, csproj);
                if (!IsLibrary(id) || version.Length == 0) continue;

                string folder = Path.Combine(packages, id.ToLowerInvariant(), version.ToLowerInvariant(), "lib");
                string? dll = Directory.Exists(folder)
                    ? Directory.EnumerateFiles(folder, id + ".dll", SearchOption.AllDirectories).OrderByDescending(path => path, StringComparer.Ordinal).FirstOrDefault()
                    : null;

                if (dll != null) assemblies[id] = dll;
                versions.Add(version);
                projects.Add(name);
            }
        }

        if (libraryRoot != null) return new LibraryLink(true, libraryRoot, GitRepository.State(libraryRoot), new Dictionary<string, string>(), [.. projects]);
        if (versions.Count == 0) return null;

        string versionText = string.Join("+", versions);
        return new LibraryLink(false, SnapshotFolder(assemblies.Keys, versionText), versionText, assemblies, [.. projects]);
    }

    /// <summary>Сборка AIFramework: <c>AI</c> или <c>AI.*</c>.</summary>
    public static bool IsLibrary(string name) => name == "AI" || name.StartsWith("AI.", StringComparison.Ordinal);

    /// <summary>Папка снимка пакетов: <c>%LOCALAPPDATA%\Atlas\lib\&lt;версия&gt;</c>, общая для всех проектов на этой версии.</summary>
    public static string SnapshotFolder(IEnumerable<string> assemblies, string version) =>
        Path.Combine(AtlasSettings.CacheRoot, "lib", $"{version}-{Hash(string.Join(",", assemblies))}");

    private static (XDocument Document, Dictionary<string, string> Properties) Evaluate(string csproj, string root)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Directory.Build.props: от корня репозитория вниз к проекту, ближний перекрывает дальний.
        var folders = new Stack<string>();
        for (string? folder = Path.GetDirectoryName(csproj); folder != null && folder.Length >= root.Length; folder = Path.GetDirectoryName(folder))
            folders.Push(folder);

        foreach (string folder in folders)
        {
            string props = Path.Combine(folder, "Directory.Build.props");
            if (File.Exists(props)) Collect(XDocument.Load(props), props, properties);
        }

        XDocument document = XDocument.Load(csproj);
        Collect(document, csproj, properties);
        return (document, properties);
    }

    private static void Collect(XDocument document, string file, Dictionary<string, string> properties)
    {
        foreach (XElement group in document.Descendants().Where(e => e.Name.LocalName == "PropertyGroup"))
            foreach (XElement property in group.Elements())
                properties[property.Name.LocalName] = Expand(property.Value, properties, file);
    }

    private static string Expand(string value, Dictionary<string, string> properties, string file)
    {
        string directory = Path.GetDirectoryName(file)! + Path.DirectorySeparatorChar;

        for (int pass = 0; pass < 8 && Property.IsMatch(value); pass++)
        {
            value = Property.Replace(value, match => match.Groups["name"].Value switch
            {
                "MSBuildThisFileDirectory" or "MSBuildProjectDirectory" => directory,
                string name => properties.GetValueOrDefault(name, ""),
            });
        }

        return value.Trim();
    }

    private static bool Generated(string root, string path) =>
        Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj" || segment.StartsWith('.'));

    private static bool Inside(string root, string path) =>
        !Path.GetRelativePath(root, path).StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(Path.GetRelativePath(root, path));

    private static string Hash(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)), 0, 4).ToLowerInvariant();
}

/// <summary>
/// Индекс библиотеки для проекта: строится один раз на версию, проект кладёт свой индекс рядом.
/// </summary>
/// <remarks>
/// У исходников снимок — собственный индекс репозитория библиотеки. Он помечен состоянием git
/// (коммит и отпечаток правок), и если состояние то же, библиотека не переиндексируется, сколько
/// бы проектов на неё ни смотрело. У пакетов снимок строится по DLL из кэша NuGet в
/// <c>%LOCALAPPDATA%\Atlas\lib\&lt;версия&gt;</c>: тел там нет, только открытое API с описаниями.
/// </remarks>
public static class LibrarySnapshot
{
    /// <summary>Файл индекса библиотеки.</summary>
    public static string IndexPath(LibraryLink link) =>
        link.Sources ? AtlasSettings.IndexPath(link.Root) : Path.Combine(link.Root, "index.db");

    /// <summary>Актуален ли снимок: построен по той же версии.</summary>
    public static bool IsCurrent(LibraryLink link)
    {
        if (!File.Exists(IndexPath(link))) return false;
        using var store = new UnitStore(IndexPath(link));
        return store.Count > 0 && store.Meta(UnitStore.StateKey) == link.Version;
    }

    /// <summary>Готовит снимок и говорит, что пришлось сделать.</summary>
    public static async Task<string> EnsureAsync(LibraryLink link, CancellationToken cancellation = default)
    {
        if (IsCurrent(link)) return $"снимок библиотеки актуален ({(link.Sources ? GitRepository.Describe(link.Version) : link.Version)}), переиндексации нет";

        using var store = new UnitStore(IndexPath(link));

        if (link.Sources)
        {
            SourceIndexer.Report report = await SourceIndexer.RunAsync(link.Root, store, cancellation: cancellation);
            return $"индекс библиотеки обновлён: разобрано файлов {report.ParsedFiles} из {report.Files} за {(report.Load + report.Scan).TotalSeconds:F0} с";
        }

        foreach ((string assembly, string dll) in link.Assemblies)
            store.ReplaceFile(new SourceFile(assembly + ".dll", link.Version, "library", null), PackageReader.Read(dll), [], []);

        store.SetMeta(UnitStore.StateKey, link.Version);
        return $"снимок пакетов {link.Version} построен: {store.Count} единиц из {link.Assemblies.Count} сборок";
    }
}
