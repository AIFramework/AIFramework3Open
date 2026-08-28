using AI.Script.Binding;
using AI.Script.Hosting;

namespace AI.Script.Std;

/// <summary>
/// Стандартная библиотека AIScript: регистрация всех модулей одним вызовом.
/// </summary>
/// <remarks>
/// Ядро языка о содержимом библиотеки не знает: здесь собран список модулей, и подключение
/// нового не требует правок ни в парсере, ни в интерпретаторе.
/// </remarks>
public static class StandardLibrary
{
    /// <summary>Модули этапов M0 и M1.</summary>
    public static IReadOnlyList<IScriptModule> Modules { get; } =
    [
        ScriptModule.FromType(typeof(CoreModule)),
        ScriptModule.FromType(typeof(MathModule)),
        ScriptModule.FromType(typeof(VecModule)),
        ScriptModule.FromType(typeof(MatModule)),
        ScriptModule.FromType(typeof(StatModule)),
        ScriptModule.FromType(typeof(StrModule)),
        ScriptModule.FromType(typeof(ReModule)),
        ScriptModule.FromType(typeof(DateModule)),
        ScriptModule.FromType(typeof(TableModule)),
        ScriptModule.FromType(typeof(IoModule)),
        ScriptModule.FromType(typeof(MlModule)),
        ScriptModule.FromType(typeof(PrepModule)),
        ScriptModule.FromType(typeof(SignalModule)),
        ScriptModule.FromType(typeof(DspModule)),
        ScriptModule.FromType(typeof(NlpModule)),
        ScriptModule.FromType(typeof(SolveModule)),
        ScriptModule.FromType(typeof(GraphModule)),
        ScriptModule.FromType(typeof(GeomModule)),
        ScriptModule.FromType(typeof(FuzzyModule)),
        ScriptModule.FromType(typeof(CtrlModule)),
    ];

    /// <summary>Регистрирует стандартную библиотеку в хосте.</summary>
    public static ScriptHost UseStandardLibrary(this ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        foreach (IScriptModule module in Modules) _ = host.Use(module);

        return host;
    }

    /// <summary>Хост со стандартной библиотекой: обычная точка входа.</summary>
    public static ScriptHost CreateHost() => new ScriptHost().UseStandardLibrary();
}
