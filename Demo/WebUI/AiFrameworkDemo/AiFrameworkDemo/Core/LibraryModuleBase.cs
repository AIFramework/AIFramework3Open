using AI.Charts;
using AI.Charts.JS;
using AiFrameworkDemo.Core;
using SkiaSharp;

namespace AiFrameworkDemo.Core;

/// <summary>
/// Базовый класс для всех модулей библиотек.
/// Берёт на себя try/catch и создание DemoResult с ошибкой.
/// Наследники реализуют только <see cref="RunCore"/>.
/// </summary>
public abstract class LibraryModuleBase : ILibraryModule
{
    public abstract string Id            { get; }
    public abstract string Name          { get; }
    public abstract string Description   { get; }
    public abstract string IconSvg       { get; }
    public abstract string Color         { get; }
    public abstract string TutorialFolder { get; }
    public abstract IReadOnlyList<CategoryDef> Categories { get; }

    public DemoResult RunDemo(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string>  textParams,
        DemoSettings settings)
    {
        try
        {
            return RunCore(algoKey, numericParams, textParams, settings);
        }
        catch (Exception ex)
        {
            return new DemoResult { Error = ex.Message };
        }
    }

    /// <summary>
    /// Реализация логики демонстратора. Исключения перехватываются в <see cref="RunDemo"/>.
    /// </summary>
    protected abstract DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string>  textParams,
        DemoSettings settings);
}
