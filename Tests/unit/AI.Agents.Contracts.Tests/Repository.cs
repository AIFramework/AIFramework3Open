using System.Reflection;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Сборка контрактов и пути репозитория для тестов.
/// </summary>
internal static class Repository
{
    /// <summary>Сборка AI.Agents.Contracts.</summary>
    public static Assembly Contracts { get; } = typeof(ToolSpec).Assembly;

    /// <summary>
    /// Корень репозитория — ближайший сверху каталог с AIFramework.sln (тот же признак, что у
    /// Tests/Directory.Build.props и PublicApi.Tests).
    /// </summary>
    public static string Root { get; } = FindRoot();

    /// <summary>Путь от корня репозитория с прямыми слешами.</summary>
    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AIFramework.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException($"Не найден корень репозитория (AIFramework.sln) выше {AppContext.BaseDirectory}.");
    }
}
