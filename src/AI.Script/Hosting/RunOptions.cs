using AI.Script.Runtime;

namespace AI.Script.Hosting;

/// <summary>Настройки одного прогона.</summary>
public sealed class RunOptions
{
    /// <summary>Имя файла для диагностики.</summary>
    public string FileName { get; set; } = "script.ais";

    /// <summary>Зерно ГСЧ прогона.</summary>
    public int Seed { get; set; }

    /// <summary>Потолки прогона.</summary>
    public ScriptLimits Limits { get; set; } = new();

    /// <summary>
    /// Доступ к файловой системе; по умолчанию запрещён.
    /// </summary>
    /// <remarks>
    /// Запрет по умолчанию, а не разрешение: хост, которому файлы не нужны, не должен
    /// открывать их случайно, забыв настроить песочницу.
    /// </remarks>
    public IScriptSandbox Sandbox { get; set; } = DeniedSandbox.Instance;

    /// <summary>
    /// Данные, подготовленные вызывающим: попадают в скрипт переменными.
    /// </summary>
    /// <remarks>
    /// Данные приходят переменными, а не текстом внутри скрипта. Пока колонку из сорока чисел
    /// модель переносила в исходник руками, точность вычислителя не спасала: цифра терялась
    /// при переносе, а не при счёте.
    /// </remarks>
    public IReadOnlyDictionary<string, object?>? Seeded { get; set; }

    /// <summary>
    /// Кэш результатов стадий; <c>null</c> — без кэша.
    /// </summary>
    /// <remarks>
    /// Владеет кэшем вызывающий, а не прогон: положив <see cref="MemoryStageCache"/> в поле
    /// хоста, он получает кэш между прогонами; создав новый на каждый запуск — только внутри
    /// прогона. Язык эту политику не выбирает, потому что она про то, зачем запускают, а не
    /// про то, что написано в скрипте.
    /// </remarks>
    public IStageCache? Cache { get; set; }

    /// <summary>
    /// Сколько ветвей исполнять одновременно при <c>core.map(parallel: true)</c>.
    /// </summary>
    /// <remarks>
    /// Единица по умолчанию: параллелизм включается тем, кто знает про свою машину и про то,
    /// потокобезопасны ли вызываемые из скрипта библиотеки. Молчаливое «по числу ядер»
    /// сделало бы поведение зависящим от машины.
    /// </remarks>
    public int Parallelism { get; set; } = 1;

    /// <summary>Приёмник сообщений о ходе работы; <c>null</c> — не сообщать.</summary>
    public IProgressSink? Progress { get; set; }

    /// <summary>
    /// Доступ к сети; по умолчанию запрещён.
    /// </summary>
    /// <remarks>
    /// Запрет по умолчанию, как и у файлов: подключение модуля <c>llm</c> к хосту — это
    /// «такая возможность есть», а не «этому скрипту можно».
    /// </remarks>
    public NetworkPolicy Network { get; set; } = NetworkPolicy.Denied;

    /// <summary>
    /// Значения, которые нельзя показывать: ключи, токены, пароли.
    /// </summary>
    /// <remarks>
    /// Маскируются в транскрипте, в артефактах и в сообщениях об отказах — везде, где текст
    /// уходит человеку или обратно в модель.
    /// </remarks>
    public IReadOnlyCollection<string>? Secrets { get; set; }

    /// <summary>
    /// Опции, которые скрипт менять не может.
    /// </summary>
    /// <remarks>
    /// Хост вправе закрепить, например, таймаут. Закреплённое значение из блока
    /// <c>options</c> не подменяется молча: скрипт получает предупреждение, потому что молчание
    /// здесь означало бы, что автор считает политику своей, а она чужая.
    /// </remarks>
    public HashSet<string> LockedOptions { get; } = new(StringComparer.Ordinal);

    /// <summary>Копия настроек.</summary>
    public RunOptions Clone()
    {
        var clone = new RunOptions
        {
            FileName = FileName,
            Seed = Seed,
            Limits = Limits.Clone(),
            Seeded = Seeded,
            Sandbox = Sandbox,
            Cache = Cache,
            Parallelism = Parallelism,
            Progress = Progress,
            Network = Network,
            Secrets = Secrets,
        };

        foreach (string name in LockedOptions) _ = clone.LockedOptions.Add(name);

        return clone;
    }
}
