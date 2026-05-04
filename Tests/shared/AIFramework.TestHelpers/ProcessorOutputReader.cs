using System;
using System.Collections.Generic;
using System.Linq;
using AI.ClassicMath.Calculator.ProcessorLogic;

namespace AIFramework.TestHelpers;

/// <summary>
/// Единый разбор консольного вывода <see cref="Processor"/> для сценариев и автотестов.
/// </summary>
public static class ProcessorOutputReader
{
    private const string ResultLinePrefix = "=> ";
    private const string CriticalErrorMarker = "КРИТИЧЕСКАЯ ОШИБКА";

    /// <summary>
    /// Возвращает текст последнего результата вида "=&gt; ..." или полный вывод, если такой строки нет.
    /// </summary>
    /// <exception cref="InvalidOperationException">Если в выводе есть критическая ошибка выполнения.</exception>
    public static string GetLastExpressionDisplay(IReadOnlyList<string> output)
    {
        ThrowIfCriticalError(output);
        var last = output.LastOrDefault(l => l.StartsWith(ResultLinePrefix, StringComparison.Ordinal));
        return last != null
            ? last.AsSpan(ResultLinePrefix.Length).Trim().ToString()
            : string.Join(Environment.NewLine, output);
    }

    /// <summary>
    /// Запускает скрипт и возвращает отображаемое значение последнего выражения.
    /// </summary>
    public static string GetLastExpressionDisplay(Processor processor, string script)
    {
        ArgumentNullException.ThrowIfNull(processor);
        return GetLastExpressionDisplay(processor.Run(script));
    }

    /// <summary>
    /// Проверяет вывод на наличие критической ошибки (для assert в тестах).
    /// </summary>
    public static bool HasCriticalError(IReadOnlyList<string> output) =>
        output.Any(l => l.Contains(CriticalErrorMarker, StringComparison.Ordinal));

    private static void ThrowIfCriticalError(IReadOnlyList<string> output)
    {
        if (!HasCriticalError(output))
            return;
        throw new InvalidOperationException(string.Join("; ", output.Where(l => l.Contains("ОШИБКА"))));
    }
}
