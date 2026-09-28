namespace AiFramework.Tools.Atlas;

/// <summary>
/// Проект, построенный на библиотеке: свой индекс поверх снимка библиотеки, ссылка на неё и
/// что из кода проекта можно отправлять в OpenRouter.
/// </summary>
public sealed class ProjectSide : IDisposable
{
    private HashSet<string>? _assemblies;

    /// <summary>Открывает индекс проекта (его может ещё не быть — тогда он пуст).</summary>
    public ProjectSide(string root, LibraryLink link, (Disclosure Level, bool Configured) disclosure)
    {
        Root = root;
        Link = link;
        Disclosure = disclosure.Level;
        DisclosureConfigured = disclosure.Configured;
        Store = new UnitStore(AtlasSettings.IndexPath(root));
    }

    /// <summary>Корень репозитория проекта.</summary>
    public string Root { get; }

    /// <summary>Имя проекта — имя папки репозитория.</summary>
    public string Name => Path.GetFileName(Root);

    /// <summary>Как проект подключает библиотеку.</summary>
    public LibraryLink Link { get; }

    /// <summary>Что из кода проекта можно отправлять.</summary>
    public Disclosure Disclosure { get; }

    /// <summary>Задано ли это в настройках явно; нет — действует <see cref="Disclosure.None"/>.</summary>
    public bool DisclosureConfigured { get; }

    /// <summary>Индекс проекта.</summary>
    public UnitStore Store { get; }

    /// <summary>Индекс построен по текущему состоянию рабочего дерева проекта.</summary>
    public bool IsCurrent => Store.Count > 0 && Store.Meta(UnitStore.StateKey) == GitRepository.State(Root);

    /// <summary>Сборка принадлежит проекту, а не библиотеке.</summary>
    public bool Owns(string assembly) => (_assemblies ??= [.. Store.Units().Select(unit => unit.Project)]).Contains(assembly);

    /// <summary>Состояние словами для <c>atlas_status</c> и отчётов.</summary>
    public string Describe() =>
        $"Проект {Name} на AIFramework: {Link.Describe()}.\n"
        + $"Индекс проекта: {(Store.Count == 0 ? "не построен — atlas project" : $"{Store.Count} единиц" + (IsCurrent ? "" : ", устарел — atlas project"))}.\n"
        + $"В OpenRouter из кода проекта уходит: {Disclosure switch { Disclosure.All => "всё", Disclosure.Names => "только сигнатуры и описания", _ => "ничего" }}"
        + (DisclosureConfigured ? "." : $" (не задано: projects.{Name} = none | names | all в settings.json).");

    /// <inheritdoc/>
    public void Dispose() => Store.Dispose();
}
