using AI.Script.Binding;

namespace AI.Script.Docs;

/// <summary>
/// Задачи, по которым сгруппированы пространства имён в индексе.
/// </summary>
/// <remarks>
/// Индекс лежит в системном промпте всегда, и строка на каждое пространство перестала в него
/// помещаться: к пятидесяти пространствам он вышел бы за бюджет. Автор скрипта к тому же ищет
/// «открыть таблицу», а не имя пространства. Поэтому индекс перечисляет задачи и их
/// пространства, а описания пространств задачи отдаёт <c>help("данные")</c>.
/// <para>
/// Список закрыт намеренно: задача, придуманная модулем «для себя», размножила бы строки
/// индекса обратно. Модуль без известной задачи попадает в <see cref="Other"/>, и это видно
/// тесту.
/// </para>
/// </remarks>
public static class ManifestGroups
{
    /// <summary>Задача для модулей, не объявивших известную.</summary>
    public const string Other = "прочее";

    /// <summary>Известные задачи в порядке индекса.</summary>
    public static IReadOnlyList<(string Name, string Description)> All { get; } =
    [
        ("основа", "числа, строки, даты, обход последовательностей"),
        ("данные", "файлы, таблицы, JSON, Parquet, SQLite, векторы, матрицы, графики"),
        ("анализ", "статистика, обучение, эконометрика, проверка ответов"),
        ("тексты", "обработка текста, языковые модели, поиск, распознавание текста и речи"),
        ("прогноз", "временные ряды, прогноз, экономика"),
        ("сигналы", "сигналы, спектры, радиоканал, изображения, звук, видео, генерация медиа"),
        ("решатели", "уравнения, оптимизация, ограничения, симуляция, графы"),
        ("отрасли", "химия, СВЧ, управление, нечеткая логика"),
    ];

    /// <summary>Задача модуля; <see cref="Other"/>, если объявленная неизвестна.</summary>
    public static string Of(IScriptModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        foreach ((string name, _) in All)
        {
            if (string.Equals(name, module.Group, StringComparison.Ordinal)) return name;
        }

        return Other;
    }

    /// <summary>Описание задачи; <c>null</c>, если такой нет.</summary>
    public static string? Describe(string group)
    {
        foreach ((string name, string description) in All)
        {
            if (string.Equals(name, group, StringComparison.Ordinal)) return description;
        }

        return string.Equals(group, Other, StringComparison.Ordinal) ? "пространства без задачи" : null;
    }

    /// <summary>
    /// Модули по задачам в порядке индекса; внутри задачи — по имени. Пустые задачи опущены.
    /// </summary>
    public static IReadOnlyList<(string Group, string Description, IReadOnlyList<IScriptModule> Modules)> Arrange(
        IReadOnlyList<IScriptModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var arranged = new List<(string, string, IReadOnlyList<IScriptModule>)>();

        foreach ((string name, string description) in All) Add(arranged, modules, name, description);

        Add(arranged, modules, Other, Describe(Other)!);

        return arranged;
    }

    private static void Add(
        List<(string, string, IReadOnlyList<IScriptModule>)> arranged,
        IReadOnlyList<IScriptModule> modules,
        string group,
        string description)
    {
        var members = new List<IScriptModule>();

        foreach (IScriptModule module in modules)
        {
            if (string.Equals(Of(module), group, StringComparison.Ordinal)) members.Add(module);
        }

        if (members.Count == 0) return;

        members.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        arranged.Add((group, description, members));
    }
}
