using System.Reflection;
using PublicApiGenerator;

namespace AIFramework.PublicApi.Tests;

/// <summary>
/// Текст публичного API сборки и его снимок на диске.
/// </summary>
internal static class ApiSnapshot
{
    /// <summary>
    /// Переменная окружения, разрешающая перезаписать снимки. Других способов обновления нет.
    /// </summary>
    public const string UpdateVariable = "AI_PUBLIC_API_UPDATE";

    private static readonly ApiGeneratorOptions Options = new()
    {
        // По умолчанию генератор пропускает типы в пространствах System.* и Microsoft.*.
        // В src таких типов сейчас нет, но если появятся, они тоже публичный API
        DenyNamespacePrefixes = [],

        // По умолчанию record печатается как class, и замена record на class (с потерей with,
        // равенства по значению и т. п.) прошла бы незамеченной
        TreatRecordsAsClasses = false,
    };

    /// <summary>
    /// Обновление снимков запрошено явно: <see cref="UpdateVariable"/> = 1.
    /// </summary>
    public static bool UpdateRequested => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    /// <summary>
    /// Тест идёт в CI (GitHub Actions выставляет CI=true): там снимки не обновляются никогда.
    /// </summary>
    public static bool RunningInCi =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Строит текст публичного API сборки из выхода теста.
    /// </summary>
    /// <param name="assemblyName">Имя сборки без расширения.</param>
    /// <returns>Текст API с переводами строк LF и одним переводом строки в конце.</returns>
    public static string Generate(string assemblyName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Сборки {assemblyName} нет в выходе теста ({path}). Проекты src подключаются шаблоном " +
                "src\\*\\*.csproj в PublicApi.Tests.csproj; проверьте, что проект лежит в src/<каталог>/ и собирается.",
                path);
        }

        // Генератор читает метаданные файла по Assembly.Location (Mono.Cecil); код сборки не исполняется
        Assembly assembly = Assembly.LoadFrom(path);
        return Normalize(assembly.GeneratePublicApi(Options));
    }

    /// <summary>
    /// Читает снимок сборки; null, если файла нет.
    /// </summary>
    public static string? Read(string assemblyName)
    {
        string path = SrcAssemblies.SnapshotPath(assemblyName);
        return File.Exists(path) ? Normalize(File.ReadAllText(path)) : null;
    }

    /// <summary>
    /// Записывает снимок сборки с переводами строк CRLF, как остальные текстовые файлы репозитория.
    /// </summary>
    public static void Write(string assemblyName, string api)
    {
        Directory.CreateDirectory(SrcAssemblies.SnapshotDirectory);
        File.WriteAllText(SrcAssemblies.SnapshotPath(assemblyName), api.Replace("\n", "\r\n"));
    }

    /// <summary>
    /// Приводит текст к одному виду: переводы строк LF, ровно один перевод строки в конце.
    /// Git может выдать файл и с CRLF, и с LF — на сравнение это не влияет.
    /// </summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd('\n') + "\n";
}
