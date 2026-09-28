namespace AiFramework.Tools.Atlas;

/// <summary>Собранные сборки проектов репозитория.</summary>
public static class BuiltAssemblies
{
    /// <summary>
    /// Самая свежая сборка проекта из <c>src/&lt;имя&gt;/bin*</c>, а если такой папки нет — из
    /// любой папки <c>bin</c> репозитория; <c>null</c> — проект не собран.
    /// </summary>
    public static string? Find(string root, string assembly)
    {
        string folder = Path.Combine(root, "src", assembly);

        return Newest(Directory.Exists(folder) ? folder : root, assembly);
    }

    private static string? Newest(string folder, string assembly) =>
        Directory.EnumerateFiles(folder, assembly + ".dll", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}bin", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
}
