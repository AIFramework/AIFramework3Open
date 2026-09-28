using AI.LLM.Services.Embeddings.OpenRouter;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiFramework.Tools.Atlas;

/// <summary>Что из кода проекта можно отправлять в OpenRouter.</summary>
public enum Disclosure
{
    /// <summary>Ничего: BM25, MinHash и исполнение работают локально, модель код проекта не видит.</summary>
    None,
    /// <summary>Сигнатуры и описания, без тел методов.</summary>
    Names,
    /// <summary>Всё, включая тела.</summary>
    All,
}

/// <summary>
/// Настройки Атласа: модели ступеней поиска и где лежат настройки и кэш.
/// </summary>
/// <remarks>
/// Всё, что относится к машине пользователя, живёт вне репозитория: репозиторий публичный, и
/// ключ, попавший в историю git, считается утёкшим. Поэтому ключ читается только из
/// переменной окружения, а <c>settings.json</c> лежит в <c>%APPDATA%\Atlas</c>: даже если
/// вписать ключ туда, он не прочитается.
/// </remarks>
public sealed class AtlasSettings
{
    /// <summary>Переменная окружения с ключом OpenRouter.</summary>
    public const string KeyVariable = "OPENROUTER_API_KEY";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Векторизатор описаний и тел методов.</summary>
    public string EmbeddingModel { get; set; } = OpenRouterEmbeddingModels.BgeM3;

    /// <summary>Реранкер между слиянием выдач и LLM; пусто — ступень выключена.</summary>
    public string? RerankModel { get; set; }

    /// <summary>LLM ступени поиска и разбиения задачи; пусто — ступень выключена.</summary>
    public string? SearchModel { get; set; }

    /// <summary>LLM вердиктов по дублям; пусто — вердикт только по порогам.</summary>
    public string? VerdictModel { get; set; }

    /// <summary>
    /// Что из кода проектов на библиотеке уходит в OpenRouter: ключ — корень репозитория
    /// проекта или имя его папки, значение — <c>none</c>, <c>names</c> или <c>all</c>.
    /// Проект без записи получает <see cref="Disclosure.None"/>: код библиотеки публичный,
    /// а код проекта может быть закрытым, и решать это пользователю, а не Атласу.
    /// </summary>
    public Dictionary<string, Disclosure> Projects { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ключ OpenRouter; <c>null</c> — сетевые ступени выключены.</summary>
    [JsonIgnore]
    public string? ApiKey { get; set; }

    /// <summary>Откуда прочитаны настройки.</summary>
    [JsonIgnore]
    public string SettingsPath { get; private set; } = DefaultSettingsPath;

    /// <summary>
    /// Настройки пользователя: папка из <c>ATLAS_CONFIG</c>; у опубликованного сервера —
    /// <c>Roaming\Atlas</c> рядом с его <c>Local\Atlas</c>; иначе <c>%APPDATA%\Atlas</c>.
    /// </summary>
    public static string ConfigRoot =>
        Environment.GetEnvironmentVariable("ATLAS_CONFIG") is { Length: > 0 } folder ? folder
        : Installed() is { } cache && Directory.GetParent(cache)?.Parent is { } appData ? Path.Combine(appData.FullName, "Roaming", "Atlas")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Atlas");

    /// <summary>
    /// Индексы и векторы: папка из <c>ATLAS_CACHE</c>; у опубликованного сервера — папка над
    /// его <c>server</c>; иначе <c>%LOCALAPPDATA%\Atlas</c>.
    /// </summary>
    /// <remarks>
    /// Приложение Claude для Windows — пакет MSIX, и AppData его процессов виртуализирована:
    /// запись в <c>%LOCALAPPDATA%\Atlas</c> физически попадает в папку пакета, а Планировщик или
    /// обычный терминал видят реальную AppData. Опубликованный сервер берёт кэш по своему месту,
    /// поэтому один и тот же exe пишет в одну папку, откуда бы его ни запустили.
    /// </remarks>
    public static string CacheRoot =>
        Environment.GetEnvironmentVariable("ATLAS_CACHE") is { Length: > 0 } folder ? folder
        : Installed() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Atlas");

    /// <summary>
    /// Файл настроек по умолчанию; если его нет, но есть в реальной <c>%APPDATA%\Atlas</c>
    /// (правка вне приложения Claude), берётся он.
    /// </summary>
    public static string DefaultSettingsPath =>
        new[] { Path.Combine(ConfigRoot, "settings.json"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Atlas", "settings.json") }
            .FirstOrDefault(File.Exists) ?? Path.Combine(ConfigRoot, "settings.json");

    /// <summary>Папка кэша опубликованного сервера (<c>…\Atlas\server\atlas.exe</c>); <c>null</c> — запущен не оттуда.</summary>
    private static string? Installed()
    {
        var server = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return server.Name.Equals("server", StringComparison.OrdinalIgnoreCase) && server.Parent is { } atlas
            && atlas.Name.Equals("Atlas", StringComparison.OrdinalIgnoreCase) ? atlas.FullName : null;
    }

    /// <summary>
    /// Отчёт о состоянии: какие ступени включены и почему. Значение ключа в отчёт не попадает.
    /// </summary>
    public string Describe()
    {
        var builder = new StringBuilder()
            .AppendLine(ApiKey != null
                ? $"Ключ OpenRouter: задан ({KeyVariable})."
                : $"Ключ OpenRouter: не задан — BGE-M3, реранкер и LLM выключены. Задайте переменную {KeyVariable}.")
            .AppendLine($"Настройки: {SettingsPath}" + (File.Exists(SettingsPath) ? "" : " (файла нет, действуют значения по умолчанию)"))
            .AppendLine($"Кэш: {CacheRoot}")
            .AppendLine(Stage("Векторизатор", EmbeddingModel, "embeddingModel"))
            .AppendLine(Stage("Реранкер", RerankModel, "rerankModel"))
            .AppendLine(Stage("LLM поиска", SearchModel, "searchModel"))
            .Append(Stage("LLM вердиктов", VerdictModel, "verdictModel"));

        return builder.ToString();
    }

    /// <summary>Что из кода проекта можно отправлять; <c>Configured</c> — задано ли это явно.</summary>
    public (Disclosure Level, bool Configured) DisclosureFor(string projectRoot)
    {
        string full = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(full);

        // Словарь из JSON сравнивает ключи с учётом регистра, а пути Windows его не учитывают.
        foreach ((string key, Disclosure level) in Projects)
        {
            string trimmed = key.TrimEnd('\\', '/');
            if (trimmed.Equals(full, StringComparison.OrdinalIgnoreCase) || trimmed.Equals(name, StringComparison.OrdinalIgnoreCase)) return (level, true);
        }

        return (Disclosure.None, false);
    }

    /// <summary>Путь к индексу репозитория в кэше.</summary>
    /// <remarks>
    /// Имя папки — имя репозитория плюс начало хеша полного пути: два клона одного
    /// репозитория не должны делить индекс, а человеку в проводнике нужно узнать свой.
    /// </remarks>
    public static string IndexPath(string repositoryRoot)
    {
        string full = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        byte[] hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(full.ToLowerInvariant()));
        string folder = $"{Path.GetFileName(full)}-{Convert.ToHexString(hash, 0, 4).ToLowerInvariant()}";

        return Path.Combine(CacheRoot, folder, "index.db");
    }

    /// <summary>Читает настройки и ключ.</summary>
    /// <param name="path">Файл настроек; <c>null</c> — <see cref="DefaultSettingsPath"/>.</param>
    public static AtlasSettings Load(string? path = null)
    {
        path ??= DefaultSettingsPath;

        AtlasSettings settings = File.Exists(path)
            ? JsonSerializer.Deserialize<AtlasSettings>(File.ReadAllText(path), JsonOptions) ?? new AtlasSettings()
            : new AtlasSettings();

        settings.SettingsPath = path;
        settings.ApiKey = ReadKey();

        return settings;
    }

    private string Stage(string name, string? model, string setting)
    {
        if (string.IsNullOrWhiteSpace(model)) return $"{name}: выключен — не задан {setting} в settings.json.";

        return ApiKey != null ? $"{name}: {model}." : $"{name}: {model}, но выключен — нет ключа.";
    }

    /// <summary>
    /// Ключ из окружения процесса, а если там пусто — из переменных пользователя.
    /// </summary>
    /// <remarks>
    /// Сервер запускает Claude Code, а тот получил окружение при своём старте: переменная,
    /// заданная позже, видна только в реестре пользователя. Без второго чтения пришлось бы
    /// перезапускать приложение после каждой смены ключа.
    /// </remarks>
    private static string? ReadKey()
    {
        string? key = Environment.GetEnvironmentVariable(KeyVariable);

        if (string.IsNullOrWhiteSpace(key) && OperatingSystem.IsWindows())
            key = Environment.GetEnvironmentVariable(KeyVariable, EnvironmentVariableTarget.User);

        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }
}
