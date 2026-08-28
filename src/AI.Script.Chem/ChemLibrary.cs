using AI.Script.Binding;
using AI.Script.Hosting;

namespace AI.Script.Chem;

/// <summary>
/// Подключение химического модуля к хосту скриптов
/// </summary>
/// <remarks>
/// Модуль живёт отдельным пакетом по той же причине, что и <c>AI.Script.Charts</c>:
/// ядру языка незачем тянуть за собой химический тулкит, а тем, кому химия нужна,
/// достаточно одного вызова. Через <c>run_script</c> модуль сразу становится
/// доступен и агенту.
/// </remarks>
public static class ChemLibrary
{
    /// <summary>Модуль <c>chem</c></summary>
    public static IScriptModule Module { get; } = ScriptModule.FromType(typeof(ChemModule));

    /// <summary>Регистрирует химический модуль в хосте</summary>
    /// <param name="host">Хост скриптов</param>
    public static ScriptHost UseChem(this ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.Use(Module);
    }
}
