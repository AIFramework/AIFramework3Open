using AI.LLM.Core.Abstractions;
using AI.LLM.Services.Embeddings.Caching;
using AI.LLM.Services.Embeddings.OpenRouter;
using AI.LLM.Services.LLM;
using AI.LLM.Services.Reranking.OpenRouter;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Индекс репозитория и поиск по нему со всеми ступенями, которые позволяют настройки.
/// </summary>
/// <remarks>
/// <para>
/// Если папка — проект, подключающий AIFramework (<see cref="LibraryLinks"/>), поиск и стеки
/// идут по снимку библиотеки, а дубли — по библиотеке и проекту вместе (<see cref="Project"/>).
/// </para>
/// Сетевые ступени включаются только при ключе OpenRouter и заданной модели. Векторный канал
/// к тому же требует, чтобы векторы уже были посчитаны (<c>atlas embed</c>): первый прогон
/// по двенадцати тысячам единиц — это время и деньги, и тратить их по первому вопросу модели
/// без ведома пользователя нельзя. После него докупаются только векторы изменившихся единиц.
/// </remarks>
public sealed class AtlasContext : IDisposable
{
    private readonly CachedEmbedder? _embedder;
    private readonly OpenRouterReranker? _reranker;
    private IReadOnlyDictionary<(string, string), int>? _usage;
    private IReadOnlyDictionary<((string, string), (string, string)), int>? _flows;
    private TypeGraph? _graph;
    private ApiSearch? _scriptSearch;
    private ApiSearch? _projectSearch;
    private ApiSearch? _search;
    private SelectionJournal? _selections;
    private DupDetector? _dups;
    private DupLedger? _ledger;
    private BehaviorHost? _behavior;
    private BehaviorCache? _behaviorCache;
    private readonly Dictionary<string, int> _layers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _assemblies = new(StringComparer.Ordinal);

    /// <summary>Слой сборок проекта: каноническая версия дубля «проект ↔ библиотека» — всегда библиотечная.</summary>
    private const int ProjectLayer = 1000;

    private AtlasContext(string root, string index, AtlasSettings settings, ProjectSide? project)
    {
        Root = root;
        Settings = settings;
        Project = project;
        Store = new UnitStore(index);

        if (settings.ApiKey is not { } key) return;

        if (!string.IsNullOrWhiteSpace(settings.EmbeddingModel))
            _embedder = new CachedEmbedder(new OpenRouterEmbedder(key, settings.EmbeddingModel), VectorCachePath(settings.EmbeddingModel));

        if (!string.IsNullOrWhiteSpace(settings.RerankModel))
            _reranker = new OpenRouterReranker(key, settings.RerankModel);

        if (!string.IsNullOrWhiteSpace(settings.SearchModel))
        {
            LlmClient = new LLMWithOpenRouterClient(new LLMOptions { ApiKey = key, ModelName = settings.SearchModel, Temperature = 0 });
            Llm = new LlmSelector(LlmClient);
        }

        if (!string.IsNullOrWhiteSpace(settings.VerdictModel))
            Judge = new DupJudge(new LLMWithOpenRouterClient(new LLMOptions { ApiKey = key, ModelName = settings.VerdictModel, Temperature = 0 }),
                Path.Combine(AtlasSettings.CacheRoot, "outbound.jsonl"));
    }

    /// <summary>Корень репозитория библиотеки (у пакетов — папка снимка).</summary>
    public string Root { get; }

    /// <summary>Проект на библиотеке; <c>null</c> — открыт сам репозиторий библиотеки.</summary>
    public ProjectSide? Project { get; }

    /// <summary>Настройки.</summary>
    public AtlasSettings Settings { get; }

    /// <summary>Индекс.</summary>
    public UnitStore Store { get; }

    /// <summary>
    /// Поиск по открытому API; строится при первом обращении: хуку после правки он не нужен,
    /// а BM25 по двенадцати тысячам единиц — это секунда.
    /// </summary>
    public ApiSearch Search
    {
        get
        {
            if (_search != null) return _search;

            _search = new ApiSearch(Store.PublicLibraryUnits(), Usage);
            _search.UseSelections(Selections.Counts);
            if (_reranker != null) _search.UseReranker(_reranker);
            return _search;
        }
    }

    /// <summary>LLM-ступень; <c>null</c> — нет ключа или не задана <c>searchModel</c>.</summary>
    public LlmSelector? Llm { get; }

    /// <summary>Вердикт моделью по серой зоне дублей; <c>null</c> — нет ключа или не задана <c>verdictModel</c>.</summary>
    public DupJudge? Judge { get; }

    /// <summary>Клиент модели поиска: разбор выдачи и разбиение задачи на шаги.</summary>
    public ILLMClient? LlmClient { get; }

    /// <summary>Сколько мест вызывают каждую единицу.</summary>
    public IReadOnlyDictionary<(string, string), int> Usage => _usage ??= Store.UsageCounts();

    /// <summary>Потоки данных по всему индексу.</summary>
    public IReadOnlyDictionary<((string, string), (string, string)), int> Flows => _flows ??= Store.FlowCounts();

    /// <summary>
    /// Детектор дублей: методы библиотеки с телами, а у проекта — ещё и методы проекта. У снимка
    /// пакетов тел нет, там сравниваются имена и описания открытого API.
    /// </summary>
    public DupDetector Dups => _dups ??= BuildDetector();

    /// <summary>
    /// Единица по идентификатору или части имени: сначала точное совпадение идентификатора,
    /// затем код проекта (в проекте спрашивают чаще о нём), затем библиотека.
    /// </summary>
    public CodeUnit? Find(string text)
    {
        CodeUnit?[] found = [Project?.Store.Find(text, limit: 1).FirstOrDefault(), Store.Find(text, limit: 1).FirstOrDefault()];
        return found.FirstOrDefault(unit => unit?.Id == text) ?? found.FirstOrDefault(unit => unit != null);
    }

    /// <summary>Поиск по методам проекта (для замысла черновика); <c>null</c> — открыта сама библиотека.</summary>
    public ApiSearch? ProjectSearch => Project is null ? null : _projectSearch ??= new ApiSearch(Project.Store.LibraryMethods(), Project.Store.UsageCounts());

    private DupDetector BuildDetector()
    {
        ShapeCache shapes = ShapeCache.Open(Path.Combine(LibraryFolder, "shapes.bin"));

        DupDetector detector = Project is null
            ? new DupDetector(Store.LibraryMethods(), Store.CalleeSets(), Usage, Layer, shapes.Of)
            : new DupDetector(
                [.. Store.LibraryMethods() is { Count: > 0 } methods ? methods : Store.PublicLibraryUnits().Where(unit => unit.Kind != "type"), .. Project.Store.LibraryMethods()],
                Merge(Store.CalleeSets(), Project.Store.CalleeSets(), (a, b) => [.. a, .. b]),
                Merge(Usage, Project.Store.UsageCounts(), (a, b) => a + b),
                Layer, shapes.Of);

        shapes.Save();
        return detector;
    }

    /// <summary>Журнал выбора после поиска: общий для библиотеки, рядом с её индексом.</summary>
    public SelectionJournal Selections => _selections ??= new SelectionJournal(Path.Combine(LibraryFolder, "selections.json"));

    /// <summary>Папка индекса библиотеки: общая для всех проектов на ней (кэш форм тел живёт здесь).</summary>
    public string LibraryFolder => Path.GetDirectoryName(Project is null ? AtlasSettings.IndexPath(Root) : LibrarySnapshot.IndexPath(Project.Link))!;

    /// <summary>Папка кэша: у проекта своя, рядом с его индексом.</summary>
    public string CacheFolder => Path.GetDirectoryName(Project is null ? AtlasSettings.IndexPath(Root) : AtlasSettings.IndexPath(Project.Root))!;

    /// <summary>Журнал решений по дублям: рядом с индексом, вне репозитория.</summary>
    public DupLedger Ledger => _ledger ??= DupLedger.Open(Path.Combine(CacheFolder, "decisions.json"));

    /// <summary>Слой проекта (см. <see cref="ProjectDeps.Layer"/>), с кэшем; сборки проекта на библиотеке — выше всех.</summary>
    public int Layer(string project)
    {
        if (!_layers.TryGetValue(project, out int layer))
            _layers[project] = layer = Project?.Owns(project) == true ? ProjectLayer + ProjectDeps.Layer(Project.Root, project) : ProjectDeps.Layer(Root, project);
        return layer;
    }

    /// <summary>Собранная сборка, где живёт единица; <c>null</c> — не собрана.</summary>
    public string? AssemblyPath(CodeUnit unit)
    {
        if (_assemblies.TryGetValue(unit.Project, out string? path)) return path;

        path = Project?.Owns(unit.Project) == true ? BuiltAssemblies.Find(Project.Root, unit.Project)
            : Project?.Link.Sources == false ? Project.Link.Assemblies.GetValueOrDefault(unit.Project)
            : BuiltAssemblies.Find(Root, unit.Project);

        // Несобранный проект ищется снова: его могут собрать, пока работает сервер.
        if (path != null) _assemblies[unit.Project] = path;
        return path;
    }

    /// <summary>Что из единицы можно отправить в OpenRouter: код библиотеки публичный, код проекта — по настройке.</summary>
    public Disclosure Disclose(CodeUnit unit) => Project?.Owns(unit.Project) == true ? Project.Disclosure : Disclosure.All;

    /// <summary>Граф типов открытого API.</summary>
    public TypeGraph Graph => _graph ??= new TypeGraph(Search.Units, Usage);

    /// <summary>
    /// Сборщик стеков: по всему открытому API или только по функциям AIScript. У второго свой
    /// BM25: среди шестисот функций языка первые места не отнимают двенадцать тысяч методов C#.
    /// </summary>
    /// <remarks>
    /// Глоссарий по умолчанию включён только для функций AIScript: там он дал +3 верных шага из
    /// 21 на бенчмарке, а на открытом API C# прироста не показал и уводил «найти» к <c>TryGet</c>.
    /// </remarks>
    public StackBuilder Stacks(bool scriptOnly, double? minCoverage = null, bool? glossary = null) => new(
        scriptOnly ? _scriptSearch ??= new ApiSearch([.. Search.Units.Where(unit => unit.Script != null)], Usage) : Search,
        Graph, Flows)
    {
        MinCoverage = minCoverage ?? 0.45,
        Glossary = glossary ?? scriptOnly,
    };

    /// <summary>
    /// Открывает индекс репозитория, которому принадлежит папка. Если это проект на
    /// AIFramework, основным индексом становится снимок библиотеки, а индекс проекта — вторым.
    /// </summary>
    public static AtlasContext Open(string folder)
    {
        string root = GitRepository.TopLevel(folder);
        AtlasSettings settings = AtlasSettings.Load();

        return LibraryLinks.Detect(root) is { } link
            ? new AtlasContext(link.Root, LibrarySnapshot.IndexPath(link), settings, new ProjectSide(root, link, settings.DisclosureFor(root)))
            : new AtlasContext(root, AtlasSettings.IndexPath(root), settings, null);
    }

    /// <summary>
    /// Подключает векторы. Без <paramref name="allowFullRun"/> — только если кэш уже есть.
    /// </summary>
    /// <returns>Что получилось, словами для отчёта.</returns>
    public async Task<string> EnableVectorsAsync(bool allowFullRun, IProgress<int>? progress = null, CancellationToken cancellation = default)
    {
        if (_embedder is null) return "векторы выключены: нет ключа или модели";
        if (!allowFullRun && _embedder.Count == 0) return "векторы не посчитаны: запустите atlas embed";

        int before = _embedder.Misses;
        Search.UseVectors(await VectorIndex.BuildAsync(Search.Units, _embedder, progress, cancellation));

        return $"векторы {Settings.EmbeddingModel}: {Search.Units.Count}, докуплено {_embedder.Misses - before}";
    }

    /// <inheritdoc/>
    /// <summary>Исполнение пар методов в отдельном процессе; поднимается при первой паре.</summary>
    public BehaviorHost Behavior => _behavior ??= new BehaviorHost(Root);

    /// <summary>Итоги исполнения, уже посчитанные для этого кода и этих сборок.</summary>
    public BehaviorCache BehaviorResults => _behaviorCache ??= new BehaviorCache(Path.Combine(CacheFolder, "behavior.json"));

    public void Dispose()
    {
        Project?.Dispose();
        _behavior?.Dispose();
        _embedder?.Dispose();
        _reranker?.Dispose();
        Store.Dispose();
    }

    private static Dictionary<TKey, TValue> Merge<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> first, IReadOnlyDictionary<TKey, TValue> second, Func<TValue, TValue, TValue> combine) where TKey : notnull
    {
        var merged = new Dictionary<TKey, TValue>(first);
        foreach ((TKey key, TValue value) in second) merged[key] = merged.TryGetValue(key, out TValue? existing) ? combine(existing, value) : value;
        return merged;
    }

    /// <summary>Кэш векторов модели: общий для всех репозиториев, ключ — отпечаток текста.</summary>
    private static string VectorCachePath(string model) =>
        Path.Combine(AtlasSettings.CacheRoot, "vectors", string.Concat(model.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '_')) + ".bin");
}
