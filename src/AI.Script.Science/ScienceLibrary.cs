using AI.Script.Binding;
using AI.Script.Hosting;

namespace AI.Script.Science;

/// <summary>
/// Подключение естественнонаучных пространств к хосту скриптов: <c>ode</c>, <c>fit</c>,
/// <c>phys</c> и <c>bio</c>.
/// </summary>
/// <remarks>
/// Все четыре пространства — тонкая привязка готовых блоков библиотеки: интегратора
/// Дормана — Принса и метода Левенберга — Марквардта из <c>AI.ClassicMath</c>, формул
/// <c>AI.Physics</c> и <c>AI.Biology</c>. Задача, на которую в учебнике одна формула, в скрипте
/// решается одним вызовом, а не пересказом формулы своими словами.
/// </remarks>
public static class ScienceLibrary
{
    /// <summary>Модули пакета в порядке регистрации.</summary>
    public static IReadOnlyList<IScriptModule> Modules { get; } =
    [
        ScriptModule.FromType(typeof(OdeModule)),
        ScriptModule.FromType(typeof(FitModule)),
        ScriptModule.FromType(typeof(PhysModule)),
        ScriptModule.FromType(typeof(BioModule)),
    ];

    /// <summary>Регистрирует пространства <c>ode</c>, <c>fit</c>, <c>phys</c> и <c>bio</c>.</summary>
    /// <param name="host">Хост скриптов.</param>
    public static ScriptHost UseScience(this ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        foreach (IScriptModule module in Modules) _ = host.Use(module);

        return host;
    }
}
