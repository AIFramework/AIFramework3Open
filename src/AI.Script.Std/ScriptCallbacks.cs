using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Вызов функции скрипта из синхронного кода библиотеки.
/// </summary>
/// <remarks>
/// Часть функций фреймворка принимает <see cref="Func{T, TResult}"/> — квадратура, поиск
/// корня, эвристика A*. Интерпретатор при этом асинхронный, и передать ему туда лямбду
/// напрямую нельзя.
/// <para>
/// Тело лямбды на языке синхронно, поэтому <c>ValueTask</c> почти всегда завершается сразу и
/// используется быстрый путь без блокировки. Ожидание остаётся на случай, когда внутри лямбды
/// вызвана асинхронная функция модуля (<c>io</c>, в будущем <c>llm</c>): это работает, но
/// стоит дорого, и злоупотреблять этим внутри численного метода не следует.
/// </para>
/// </remarks>
internal static class ScriptCallbacks
{
    /// <summary>Вызывает функцию скрипта и требует числовой результат.</summary>
    public static double Number(IScriptContext context, ScriptCallable callable, string what, params double[] arguments)
    {
        var values = new ScriptValue[arguments.Length];

        for (int i = 0; i < arguments.Length; i++) values[i] = ScriptValue.Num(arguments[i]);

        return Invoke(context, callable, values).AsNumber(what);
    }

    /// <summary>Вызывает функцию скрипта и отдаёт результат как значение языка.</summary>
    public static ScriptValue Invoke(IScriptContext context, ScriptCallable callable, params ScriptValue[] arguments)
    {
        ArgumentNullException.ThrowIfNull(callable);

        context.Cancellation.ThrowIfCancellationRequested();

        ValueTask<ScriptValue> task = context.CallAsync(ScriptValue.Fn(callable), arguments);

        return task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>Превращает функцию скрипта в обычный делегат одного аргумента.</summary>
    public static Func<double, double> AsFunction(IScriptContext context, ScriptCallable callable, string what) =>
        x => Number(context, callable, what, x);

    /// <summary>Отказ с указанием функции, из которой пришёл вызов.</summary>
    public static ScriptError Failed(string what, string message) =>
        new(DiagnosticCodes.FunctionFailed, $"{what}: {message}");
}
