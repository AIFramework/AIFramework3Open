namespace AI.Script.Hosting;

/// <summary>Почему прогон встал.</summary>
public enum DebugStop
{
    /// <summary>Точка останова на строке.</summary>
    Breakpoint,

    /// <summary>Закончился шаг: с заходом, с обходом или с выходом.</summary>
    Step,

    /// <summary>Попросили паузу.</summary>
    Pause,

    /// <summary>Инструкция бросила ошибку.</summary>
    Error,
}

/// <summary>
/// Сеанс отладки: точки останова по строкам, шаги, пауза — то, что среда показывает кнопками
/// «Продолжить», «Шаг с заходом», «Шаг с обходом», «Шаг с выходом».
/// </summary>
/// <remarks>
/// Остановка — «всё стоит»: пока одна ветвь параллельного участка на паузе, остальные ждут на
/// своей ближайшей инструкции. Иначе на паузе менялись бы общие данные, и смотреть на них было бы
/// бессмысленно. Шаги считаются в ветви, где прогон встал.
/// <para>
/// Ожидание на паузе слушает отмену прогона: «Стоп» снимает паузу и останавливает прогон.
/// </para>
/// </remarks>
public sealed class DebugSession : IScriptDebugger
{
    private enum Mode
    {
        Run,
        Into,
        Over,
        Out,
    }

    /// <summary>Как часто ожидание на паузе проверяет отмену прогона.</summary>
    private const int CancellationPollMilliseconds = 50;

    private readonly object _sync = new();
    private readonly HashSet<int> _lines = [];
    private Mode _mode;
    private int _stepThread;
    private int _stepDepth;
    private bool _pauseRequested;
    private DebugContext? _current;

    /// <summary>Вставать на ошибке, ещё до того, как её поймает <c>try</c> или прогон.</summary>
    public bool BreakOnError { get; set; } = true;

    /// <summary>
    /// Прогон встал. Приходит в потоке прогона, до ожидания: обработчик вправе сразу же дать
    /// следующую команду (<see cref="Continue"/>, шаг) — тогда прогон не задержится вовсе.
    /// </summary>
    public event Action<DebugContext, DebugStop>? Paused;

    /// <summary>Прогон пошёл дальше после паузы.</summary>
    public event Action? Resumed;

    /// <summary>Где стоит прогон; <c>null</c> — не на паузе.</summary>
    public DebugContext? Current
    {
        get { lock (_sync) return _current; }
    }

    /// <summary>Строки с точками останова.</summary>
    public IReadOnlyCollection<int> Breakpoints
    {
        get { lock (_sync) return _lines.ToArray(); }
    }

    /// <summary>Заменяет точки останова; действует и посреди прогона.</summary>
    public void SetBreakpoints(IEnumerable<int> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        lock (_sync)
        {
            _lines.Clear();
            _lines.UnionWith(lines);
        }
    }

    public void Continue() => Resume(Mode.Run);

    /// <summary>Следующая инструкция, в том числе внутри вызываемой функции.</summary>
    public void StepInto() => Resume(Mode.Into);

    /// <summary>Следующая инструкция этого кадра или вызывающего — вызовы проходятся целиком.</summary>
    public void StepOver() => Resume(Mode.Over);

    /// <summary>До возврата из текущей функции.</summary>
    public void StepOut() => Resume(Mode.Out);

    /// <summary>Встать на ближайшей инструкции.</summary>
    public void Pause()
    {
        lock (_sync) _pauseRequested = true;
    }

    /// <inheritdoc />
    public void OnEvent(DebugContext context)
    {
        DebugStop reason;

        lock (_sync)
        {
            // Пока другая ветвь на паузе, эта ждёт: «всё стоит».
            while (_current != null) Wait(context);

            if (!ShouldStop(context, out reason)) return;

            _current = context;
            _mode = Mode.Run;
            _pauseRequested = false;
        }

        Paused?.Invoke(context, reason);

        lock (_sync)
        {
            while (ReferenceEquals(_current, context)) Wait(context);
        }

        Resumed?.Invoke();
    }

    private bool ShouldStop(DebugContext context, out DebugStop reason)
    {
        reason = DebugStop.Step;

        if (context.Kind == DebugEventKind.Error)
        {
            reason = DebugStop.Error;
            return BreakOnError;
        }

        if (_pauseRequested)
        {
            reason = DebugStop.Pause;
            return true;
        }

        bool sameThread = context.Thread == _stepThread;
        int depth = context.Frame.Depth;

        if (_mode == Mode.Into && sameThread) return true;
        if (_mode == Mode.Over && sameThread && depth <= _stepDepth) return true;
        if (_mode == Mode.Out && sameThread && depth < _stepDepth) return true;

        reason = DebugStop.Breakpoint;
        return _lines.Count > 0 && _lines.Contains(context.Line);
    }

    private void Resume(Mode mode)
    {
        lock (_sync)
        {
            if (_current == null) return;

            _mode = mode;
            _stepThread = _current.Thread;
            _stepDepth = _current.Frame.Depth;
            _current = null;
            Monitor.PulseAll(_sync);
        }
    }

    /// <summary>Ждёт под замком, проверяя отмену прогона.</summary>
    private void Wait(DebugContext context)
    {
        Monitor.Wait(_sync, CancellationPollMilliseconds);

        if (context.Cancellation.IsCancellationRequested)
        {
            _current = null;
            Monitor.PulseAll(_sync);
            context.Cancellation.ThrowIfCancellationRequested();
        }
    }
}
