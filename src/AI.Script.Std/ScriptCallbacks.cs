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
/// Тело лямбды на языке синхронно, поэтому <c>ValueTask</c> завершается сразу, и мост работает
/// без ожидания. Лямбда, которая ВНУТРИ численного метода обращается к сети либо к диску,
/// получает отказ, а не молчаливую блокировку потока: численный метод зовёт её тысячами, и
/// каждый такой вызов занял бы поток пула ожиданием — восемь ветвей опыта складывались бы в
/// восемь занятых потоков вместо восьми одновременных запросов.
/// </para>
/// <para>
/// Там, где ждать уместно — <c>core.map</c>, <c>core.filter</c>, <c>exp.run</c>, — моста нет
/// вовсе: функция модуля сама асинхронна и ждёт <c>CallEachAsync</c>.
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

        if (task.IsCompletedSuccessfully) return task.Result;

        // Незавершённая задача здесь означает ровно одно: лямбда ушла в сеть либо на диск.
        // Дождаться её значило бы занять поток пула на время запроса — и так на каждом шаге
        // численного метода.
        if (!task.IsCompleted)
        {
            throw new ScriptError(
                DiagnosticCodes.FunctionFailed,
                "функция, переданная численному методу, обратилась к сети либо к файлам",
                "численный метод зовёт её тысячи раз подряд: прочитайте данные заранее и "
                + "передайте значениями, а сетевые вызовы делайте через core.map либо exp.run");
        }

        // Задача завершена отказом — пусть отказ и выйдет наружу, не потеряв причины.
        return task.GetAwaiter().GetResult();
    }

    /// <summary>Превращает функцию скрипта в обычный делегат одного аргумента.</summary>
    public static Func<double, double> AsFunction(IScriptContext context, ScriptCallable callable, string what) =>
        x => Number(context, callable, what, x);

    /// <summary>Отказ с указанием функции, из которой пришёл вызов.</summary>
    public static ScriptError Failed(string what, string message) =>
        new(DiagnosticCodes.FunctionFailed, $"{what}: {message}");
}
