using AI.Script.Binding;
using AI.Script.Hosting;

namespace AI.Script.Data;

/// <summary>
/// Подключение пространства <c>data</c> к хосту.
/// </summary>
/// <remarks>Отдельным вызовом: Parquet.Net и нативный SQLite нужны не каждому хосту.</remarks>
public static class DataLibrary
{
    /// <summary>Модуль <c>data</c>.</summary>
    public static IScriptModule Module { get; } = ScriptModule.FromType(typeof(DataModule));

    /// <summary>Подключает чтение Parquet и SQLite.</summary>
    /// <param name="host">Хост.</param>
    public static ScriptHost UseData(this ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Use(Module);
    }
}
