using System.Xml.Linq;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Что придётся подключить ради стека: проекты с их зависимостями, пакеты и нативные части.
/// </summary>
/// <remarks>
/// Читается прямо из <c>.csproj</c>: <c>ProjectReference</c> дают замыкание проектов,
/// <c>PackageReference</c> — пакеты. Нативная часть отмечается отдельно: она определяет,
/// заработает ли стек на другой платформе (Faiss собран только под win-x64).
/// </remarks>
public static class ProjectDeps
{
    private static readonly string[] NativeMarkers = ["OnnxRuntime", "SkiaSharp", "CUDA", "Torch", "OpenCv", "e_sqlite3", "MKL", "Faiss"];

    /// <summary>Одна строка для отчёта.</summary>
    public static string Describe(string root, IEnumerable<string> assemblies)
    {
        string[] direct = [.. assemblies.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        (SortedSet<string> closure, SortedSet<string> packages) = Closure(root, direct);

        string[] pulled = [.. closure.Except(direct)];
        string[] native = [.. packages.Concat(closure).Where(name => NativeMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase)))];

        return $"подключить: {string.Join(", ", direct)}"
            + (pulled.Length > 0 ? $"; с ними приедут: {string.Join(", ", pulled)}" : "")
            + (packages.Count > 0 ? $"; пакеты: {string.Join(", ", packages.Where(p => p.Length > 0))}" : "")
            + (native.Length > 0 ? $"; нативное: {string.Join(", ", native)}" : "");
    }

    /// <summary>
    /// Слой проекта: сколько проектов в его замыкании зависимостей. У ядра <c>AI</c> — один,
    /// у предметных сборок — больше; каноническая версия дубля берётся из нижнего слоя.
    /// </summary>
    public static int Layer(string root, string project) => Closure(root, [project]).Projects.Count;

    private static (SortedSet<string> Projects, SortedSet<string> Packages) Closure(string root, IEnumerable<string> direct)
    {
        var closure = new SortedSet<string>(StringComparer.Ordinal);
        var packages = new SortedSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(direct);

        while (queue.TryDequeue(out string? project))
        {
            if (!closure.Add(project)) continue;

            string file = Path.Combine(root, "src", project, project + ".csproj");
            if (!File.Exists(file)) continue;

            XDocument xml = XDocument.Load(file);

            foreach (XElement reference in xml.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
                queue.Enqueue(Path.GetFileNameWithoutExtension((string?)reference.Attribute("Include") ?? ""));

            foreach (XElement package in xml.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
                packages.Add((string?)package.Attribute("Include") ?? "");
        }

        return (closure, packages);
    }
}
