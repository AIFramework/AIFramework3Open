using AI.Script.Runtime;
using AI.Script.Syntax;

namespace AI.Script.Hosting;

/// <summary>Что произошло в прогоне, о котором сообщают отладчику.</summary>
public enum DebugEventKind
{
    /// <summary>Сейчас выполнится инструкция (или тело лямбды-выражения).</summary>
    Statement,

    /// <summary>Инструкция бросила ошибку — до того, как её поймает <c>try</c> или прогон.</summary>
    Error,
}

/// <summary>
/// Отладчик прогона: интерпретатор зовёт его перед каждой инструкцией и при ошибке.
/// </summary>
/// <remarks>
/// Метод синхронный и вправе блокировать — так отладчик ставит прогон на паузу. Ожидание через
/// <c>await</c> не годится: численные методы (интегрирование, поиск корня) зовут функции
/// скрипта синхронно, и пауза внутри такой функции стала бы ошибкой прогона. Поэтому хост,
/// подключающий отладчик, запускает прогон не в потоке интерфейса.
/// <para>
/// Реализация обязана быть потокобезопасной: при <c>core.map(parallel: true)</c> события
/// приходят из нескольких ветвей одновременно (<see cref="DebugContext.Thread"/>).
/// </para>
/// </remarks>
public interface IScriptDebugger
{
    /// <summary>Событие прогона; контекст действителен только во время вызова.</summary>
    void OnEvent(DebugContext context);
}

/// <summary>
/// Кадр стека вызовов: скрипт, функция, стадия или лямбда. Связан с вызывающим кадром, так что
/// стек — это цепочка <see cref="Caller"/>.
/// </summary>
public sealed class DebugFrame
{
    internal DebugFrame(string name, bool isStage, TextSpan callSite, DebugFrame? caller, Scope scope)
    {
        Name = name;
        IsStage = isStage;
        CallSite = callSite;
        Caller = caller;
        Depth = caller == null ? 0 : caller.Depth + 1;
        Scope = scope;
    }

    /// <summary>Имя функции или стадии; у верхнего кадра — «скрипт», у лямбды — «лямбда».</summary>
    public string Name { get; }

    public bool IsStage { get; }

    /// <summary>Место вызова в вызывающем кадре; пусто, когда функцию позвал модуль (<c>core.map</c>).</summary>
    public TextSpan CallSite { get; }

    public DebugFrame? Caller { get; }

    /// <summary>Глубина: 0 — сам скрипт.</summary>
    public int Depth { get; }

    /// <summary>Область имён текущей инструкции кадра.</summary>
    public Scope Scope { get; internal set; }

    /// <summary>Текущая инструкция кадра.</summary>
    public TextSpan Span { get; internal set; }

    /// <summary>Стек от этого кадра к скрипту.</summary>
    public IEnumerable<DebugFrame> Stack()
    {
        for (DebugFrame? frame = this; frame != null; frame = frame.Caller)
            yield return frame;
    }
}

/// <summary>
/// Событие для отладчика: где стоит прогон, чьи переменные видны, как вычислить выражение.
/// </summary>
public sealed class DebugContext
{
    private readonly Interpreter _interpreter;

    internal DebugContext(DebugEventKind kind, Interpreter interpreter, DebugFrame frame, int thread, int? branch, ScriptError? error, SourceText? source, CancellationToken cancellation)
    {
        Kind = kind;
        _interpreter = interpreter;
        Frame = frame;
        Thread = thread;
        Branch = branch;
        Error = error;
        Source = source;
        Cancellation = cancellation;
    }

    public DebugEventKind Kind { get; }

    /// <summary>Текущий кадр; стек — <see cref="DebugFrame.Stack"/>.</summary>
    public DebugFrame Frame { get; }

    /// <summary>Ветвь исполнения: 0 — основная, больше нуля — ветви параллельного участка.</summary>
    public int Thread { get; }

    /// <summary>Номер элемента в <c>core.map(parallel: true)</c>; <c>null</c> — не в параллельном участке.</summary>
    public int? Branch { get; }

    /// <summary>Ошибка для <see cref="DebugEventKind.Error"/>.</summary>
    public ScriptError? Error { get; }

    public SourceText? Source { get; }

    /// <summary>Отмена прогона: ожидание на паузе обязано её слушать, иначе «Стоп» не остановит.</summary>
    public CancellationToken Cancellation { get; }

    /// <summary>Строка текущей инструкции, с единицы; 0 — текст скрипта неизвестен.</summary>
    public int Line => LineOf(Frame.Span);

    /// <summary>Строка места в тексте скрипта, с единицы; 0 — неизвестно.</summary>
    public int LineOf(TextSpan span) => Source == null || span.Length == 0 && span.Start == 0 ? 0 : Source.GetLinePosition(span.Start).Line;

    /// <summary>
    /// Переменные кадра — от внутренней области к внешней, без встроенных констант и функций;
    /// затенённое имя показывается один раз, ближайшим значением.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, ScriptValue>> Locals(DebugFrame? frame = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<KeyValuePair<string, ScriptValue>>();

        for (Scope? scope = (frame ?? Frame).Scope; scope != null; scope = scope.Parent)
        {
            foreach (KeyValuePair<string, ScriptValue> entry in scope.Entries)
            {
                if (!seen.Add(entry.Key) || ScriptConstants.All.ContainsKey(entry.Key) || entry.Value.Type == ScriptType.Fn) continue;

                result.Add(entry);
            }
        }

        return result;
    }

    /// <summary>Вычисляет выражение в области кадра — «контрольное значение» отладчика.</summary>
    /// <exception cref="ScriptError">Выражение не разобралось или не вычислилось.</exception>
    public ScriptValue Evaluate(string expression, DebugFrame? frame = null) =>
        _interpreter.EvaluateWatch(expression, (frame ?? Frame).Scope);
}
