using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Hosting;

/// <summary>
/// Состояние одного прогона: вывод, результаты, счётчики, ГСЧ.
/// </summary>
/// <remarks>
/// Всё, что живёт ровно один прогон, собрано здесь и нигде больше. Глобальной статики нет
/// намеренно: два прогона в одном процессе не должны влиять друг на друга, иначе
/// воспроизводимость держится на порядке запуска, а не на зерне.
/// </remarks>
public sealed class RunContext : IScriptContext
{
    private readonly FunctionRegistry _registry;
    private readonly object _sync = new();

    /// <summary>
    /// Подставленный ГСЧ параллельной ветви.
    /// </summary>
    /// <remarks>
    /// <see cref="AsyncLocal{T}"/>, а не поле: при <c>core.map(parallel: true)</c> каждая
    /// ветвь получает свой поток случайных чисел, выведенный из зерна прогона и номера
    /// элемента. Общий ГСЧ на всех дал бы результат, зависящий от того, какой поток успел
    /// первым, — то есть невоспроизводимый.
    /// </remarks>
    private readonly AsyncLocal<Random?> _branchRandom = new();

    private readonly AsyncLocal<CancellationToken?> _branchCancellation = new();

    private Random _random;
    private int _calls;

    /// <summary>Создаёт состояние прогона.</summary>
    public RunContext(
        RunOptions options,
        FunctionRegistry registry,
        DiagnosticBag diagnostics,
        CancellationToken cancellation)
    {
        Options = options;
        _registry = registry;
        Diagnostics = diagnostics;
        RunCancellation = cancellation;
        Seed = options.Seed;
        _random = new Random(options.Seed);
        Counters = new LimitCounters(options.Limits);
        Cache = options.Cache ?? DisabledStageCache.Instance;
        Progress = options.Progress;
        Parallelism = Math.Max(1, options.Parallelism);
        Network = options.Network ?? NetworkPolicy.Denied;
        Secrets = new SecretMask(options.Secrets);
    }

    /// <summary>Настройки прогона.</summary>
    public RunOptions Options { get; }

    /// <summary>Диагностика прогона.</summary>
    public DiagnosticBag Diagnostics { get; }

    /// <summary>Отмена всего прогона.</summary>
    public CancellationToken RunCancellation { get; }

    /// <inheritdoc/>
    public CancellationToken Cancellation => _branchCancellation.Value ?? RunCancellation;

    /// <summary>Кэш результатов стадий.</summary>
    public IStageCache Cache { get; }

    /// <summary>Приёмник сообщений о ходе работы; <c>null</c>, если хосту это не нужно.</summary>
    public IProgressSink? Progress { get; }

    /// <summary>Граф вызовов стадий.</summary>
    public RunGraph Graph { get; } = new();

    /// <inheritdoc/>
    public int Parallelism { get; }

    /// <inheritdoc/>
    public Random Random => _branchRandom.Value ?? _random;

    /// <inheritdoc/>
    public int Seed { get; private set; }

    /// <summary>
    /// Подставляет отмену для текущей ветви исполнения и возвращает прежнюю.
    /// </summary>
    /// <remarks>
    /// Нужно для <c>@timeout</c> стадии: внутри неё отменой считается более ранний из двух
    /// сроков, а снаружи — прежний.
    /// </remarks>
    public CancellationToken PushCancellation(CancellationToken cancellation)
    {
        CancellationToken previous = Cancellation;

        _branchCancellation.Value = cancellation;

        return previous;
    }

    /// <summary>Возвращает прежнюю отмену ветви.</summary>
    public void PopCancellation(CancellationToken previous) =>
        _branchCancellation.Value = previous == RunCancellation ? null : previous;

    /// <summary>
    /// Даёт ветви собственный ГСЧ, выведенный из зерна прогона и номера ветви.
    /// </summary>
    /// <remarks>
    /// Возвращает объект, восстанавливающий прежнее состояние: подстановка обязана быть
    /// парной, иначе случайные числа после параллельного участка зависели бы от него.
    /// </remarks>
    public IDisposable UseBranchRandom(int branch)
    {
        Random? previous = _branchRandom.Value;

        _branchRandom.Value = new Random(BranchSeed(Seed, branch));

        return new BranchScope(this, previous);
    }

    /// <summary>
    /// Зерно ветви по зерну прогона и номеру элемента.
    /// </summary>
    /// <remarks>
    /// Собственное перемешивание, а не <see cref="HashCode.Combine{T1, T2}"/>: тот засеян
    /// случайно при старте процесса, поэтому давал бы разные числа при каждом запуске
    /// утилиты — то есть ровно ту невоспроизводимость, ради устранения которой ветви и
    /// получают отдельный поток. Множители — из перемешивания Кнута, важна здесь только
    /// устойчивость результата от запуска к запуску.
    /// </remarks>
    public static int BranchSeed(int seed, int branch)
    {
        unchecked
        {
            uint mixed = (uint)seed * 2654435761u;

            mixed ^= (uint)branch * 2246822519u;
            mixed = (mixed << 13) | (mixed >> 19);
            mixed *= 3266489917u;

            return (int)(mixed & 0x7FFFFFFF);
        }
    }

    private sealed class BranchScope : IDisposable
    {
        private readonly RunContext _context;
        private readonly Random? _previous;

        public BranchScope(RunContext context, Random? previous)
        {
            _context = context;
            _previous = previous;
        }

        public void Dispose() => _context._branchRandom.Value = _previous;
    }

    /// <inheritdoc/>
    public IScriptSandbox Sandbox => Options.Sandbox;

    /// <inheritdoc/>
    public NetworkPolicy Network { get; }

    /// <summary>Маска секретов прогона.</summary>
    public SecretMask Secrets { get; }

    /// <inheritdoc/>
    public void BeginExternalCall() => Counters.BeginExternalCall();

    /// <inheritdoc/>
    public void CountExternal(long tokens = 0, decimal cost = 0) => Counters.CountExternal(tokens, cost);

    /// <inheritdoc/>
    public ExternalUsage Usage =>
        new(Counters.ExternalCalls, Counters.ExternalTokens, Counters.ExternalCost);

    /// <summary>Счётчики лимитов.</summary>
    public LimitCounters Counters { get; }

    /// <summary>Напечатанное скриптом.</summary>
    public List<string> Transcript { get; } = [];

    /// <summary>Именованные результаты.</summary>
    public Dictionary<string, object?> Emitted { get; } = new(StringComparer.Ordinal);

    /// <summary>Артефакты.</summary>
    public List<ScriptArtifact> Artifacts { get; } = [];

    /// <summary>Сколько вызовов функций сделано.</summary>
    public int Calls => _calls;

    /// <summary>Учитывает вызов функции.</summary>
    /// <remarks>Через <see cref="Interlocked"/>: на параллельном участке считают все ветви.</remarks>
    public void CountCall() => Interlocked.Increment(ref _calls);

    /// <inheritdoc/>
    public IReadOnlyList<IScriptModule> Modules => _registry.Modules;

    /// <summary>Интерпретатор прогона; нужен функциям высшего порядка.</summary>
    public Interpreter? Interpreter { get; set; }

    /// <summary>Переопределяет зерно из блока <c>options</c>.</summary>
    public void Reseed(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    /// <summary>
    /// Печатает строку в транскрипт.
    /// </summary>
    /// <remarks>
    /// Под замком: при <c>core.map(parallel: true)</c> печатать могут несколько ветвей сразу, а
    /// <see cref="List{T}"/> одновременной записи не переживает. Порядок строк в этом случае
    /// зависит от планировщика — это цена параллельного участка, и она обозначена в справке
    /// <c>core.map</c>.
    /// </remarks>
    public void Print(string line)
    {
        string safe = Secrets.Apply(line);

        lock (_sync) Transcript.Add(safe);
    }

    /// <summary>
    /// Показывает значение пользователю.
    /// </summary>
    /// <remarks>
    /// Вид артефакта определяет само значение, если оно умеет
    /// (<see cref="IScriptArtifactSource"/>): ядро не знает ни про графики, ни про
    /// изображения, но обязано донести их до хоста не как «строку».
    /// </remarks>
    public void Show(ScriptValue value)
    {
        string text = Secrets.Apply(ScriptFormatter.Format(value));

        ScriptArtifact artifact =
            value.Type == ScriptType.Handle && value.AsHandle().Target is IScriptArtifactSource source
                ? new ScriptArtifact
                {
                    Kind = source.ArtifactKind,
                    Title = string.IsNullOrWhiteSpace(source.ArtifactTitle) ? null : source.ArtifactTitle,
                    Text = text,
                    Value = source.ArtifactPayload,
                }
                : new ScriptArtifact
                {
                    Kind = value.Type == ScriptType.Table ? "table" : "value",
                    Text = text,
                    Value = Marshaller.Unwrap(value),
                };

        lock (_sync)
        {
            Transcript.Add(text);
            Artifacts.Add(artifact);
        }
    }

    /// <inheritdoc/>
    public void CountStep() => Counters.CountStep();

    /// <inheritdoc/>
    public void CountAllocation(long elements) => Counters.CountAllocation(elements);

    /// <inheritdoc/>
    public ScriptFunction? FindFunction(string fullName) => _registry.Find(fullName);

    /// <inheritdoc/>
    public ValueTask<ScriptValue> CallAsync(ScriptValue callable, params ScriptValue[] arguments) =>
        Required().CallAsync(callable, arguments);

    /// <inheritdoc/>
    public ValueTask<ScriptValue[]> CallEachAsync(
        ScriptValue callable,
        IReadOnlyList<ScriptValue> items,
        int parallelism) =>
        Required().CallEachAsync(callable, items, parallelism);

    private Interpreter Required() =>
        Interpreter ?? throw new ScriptError(
            DiagnosticCodes.FunctionFailed,
            "обратный вызов недоступен вне прогона");
}
