using AI.Script.Binding;
using AI.Script.Hosting;

namespace AI.Script.Nn;

/// <summary>
/// Подключение пространства <c>nn</c> к хосту.
/// </summary>
/// <remarks>
/// Отдельным вызовом: хост, которому нейросети не нужны, не тянет OpenBlasSharp с нативной
/// библиотекой под каждую платформу.
/// </remarks>
public static class NnLibrary
{
    /// <summary>Модуль <c>nn</c>.</summary>
    public static IScriptModule Module { get; } = ScriptModule.FromType(typeof(NnModule));

    /// <summary>Подключает нейронные сети.</summary>
    /// <param name="host">Хост.</param>
    public static ScriptHost UseNeuralNetworks(this ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Use(Module);
    }
}
