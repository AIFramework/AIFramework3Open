using AI.Script.Binding;
using AI.Script.Hosting;

namespace AI.Script.Vision;

/// <summary>
/// Подключение пространства <c>cv</c> к хосту.
/// </summary>
/// <remarks>
/// Отдельным вызовом, как графики и LLM-контур: хост, которому изображения не нужны, не тянет
/// SkiaSharp с нативными библиотеками под каждую платформу.
/// </remarks>
public static class VisionLibrary
{
    /// <summary>Модуль <c>cv</c>.</summary>
    public static IScriptModule Module { get; } = ScriptModule.FromType(typeof(CvModule));

    /// <summary>Подключает обработку изображений.</summary>
    /// <param name="host">Хост.</param>
    public static ScriptHost UseVision(this ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Use(Module);
    }
}
