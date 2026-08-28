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
    /// <summary>
    /// Модули стандартной библиотеки.
    /// </summary>
    /// <remarks>
    /// Здесь только то, что не добавляет сборке зависимостей: графики, LLM-контур и химия
    /// подключаются отдельными вызовами, потому что тянут за собой чужой код. Экономика и СВЧ
    /// живут здесь — их собственные зависимости и так уже подключены.
    /// </remarks>
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
        ScriptModule.FromType(typeof(EconModule)),
        ScriptModule.FromType(typeof(MwModule)),
        ScriptModule.FromType(typeof(LogicModule)),
        ScriptModule.FromType(typeof(SiglabModule)),
        ScriptModule.FromType(typeof(ExplainModule)),
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
