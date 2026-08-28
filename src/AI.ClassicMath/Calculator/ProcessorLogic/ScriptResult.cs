using System;
using System.Collections.Generic;

namespace AI.ClassicMath.Calculator.ProcessorLogic;

/// <summary>
/// Исход прогона скрипта: что напечатано и почему прервано.
/// </summary>
/// <param name="Ok">Скрипт доработал до конца.</param>
/// <param name="Output">Напечатанное скриптом — в том числе то, что успело напечататься до срыва.</param>
/// <param name="Error">Причина отказа; <c>null</c> при успехе.</param>
/// <param name="Emitted">
/// Именованные результаты, объявленные скриптом через <c>emit</c>. Пусто, если он их не объявлял.
/// </param>
[Serializable]
public sealed record ScriptResult(
    bool Ok,
    IReadOnlyList<string> Output,
    string Error,
    IReadOnlyDictionary<string, object> Emitted = null)
{
    /// <summary>Напечатанное одним текстом.</summary>
    public string Text => string.Join("\n", Output);
}
