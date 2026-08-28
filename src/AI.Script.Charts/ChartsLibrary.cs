using AI.Script.Binding;
using AI.Script.Hosting;

namespace AI.Script.Charts;

/// <summary>
/// Подключение пространства <c>plot</c> к хосту.
/// </summary>
/// <remarks>
/// Отдельная сборка от <c>AI.Script.Std</c>: графики тянут <c>AI.Charts.JS</c>, а ядро языка
/// обязано оставаться пригодным там, где рисовать нечем и незачем.
/// </remarks>
public static class ChartsLibrary
{
    /// <summary>Модуль графиков.</summary>
    public static IScriptModule Module { get; } = ScriptModule.FromType(typeof(PlotModule));

    /// <summary>Регистрирует пространство <c>plot</c> в хосте.</summary>
    public static ScriptHost UseCharts(this ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Use(Module);
    }
}
