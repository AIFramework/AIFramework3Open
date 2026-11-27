using System.Reflection;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents.ReAct.Interop;
using AI.LLM.Agents.Tools;
using Serilog;
using AI.LLM.Agents.ReAct.Tools;

namespace AI.Agents.Interop.Effects;

/// <summary>
/// Инструменты, объявленные атрибутом <c>[AgentTool]</c> (AI.LLM), под контрактом <see cref="IToolSet"/>: вид действия
/// и «повтор безопасен» берутся из <see cref="ToolEffectAttribute"/> на том же методе.
/// </summary>
/// <remarks>
/// <para>
/// Исполнение и схема — те же, что у <c>ToolRegistry</c> и <c>ToolRegistryToolSource</c> из AI.LLM: реестр
/// собирается из тех же объектов и ничего не знает об атрибуте эффекта. Имя инструмента и метод берутся из
/// закрытого <c>ToolRegistry.ScanMethods</c> (через <c>InternalsVisibleTo</c>), поэтому правило имени одно.
/// </para>
/// <para>
/// Итог метода. Метод может вернуть итог контракта (<see cref="ToolResult"/> или <c>Task</c> с ним): он проходит целиком —
/// статус, код из <see cref="ToolCodes"/>, <see cref="ToolResult.Handle"/>, подтверждения. Итог AI.LLM с признаком
/// успеха (<c>ToolResult.Success</c>/<c>Failure</c>) дает <see cref="ToolStatus.Ok"/> или <see cref="ToolStatus.Failed"/>,
/// исключение метода — <see cref="ToolStatus.Failed"/>. Строку правило <see cref="ToolTextResult"/> считает удачей
/// (<see cref="ToolTextResult.Success"/>) или запрещает уже при построении набора (<see cref="ToolTextResult.Refused"/>).
/// </para>
/// <para>
/// Это единственный файл сборки, которому разрешено отражение (храповик <c>NoReflectionTests</c>, исключение с
/// причиной): чтение пользовательского атрибута с <see cref="MethodInfo"/> и поиск методов с
/// <see cref="ToolEffectAttribute"/> без <c>[AgentTool]</c>. Члены AI.LLM через отражение не вызываются.
/// </para>
/// </remarks>
public static class AttributedTools
{
    /// <summary>Набор инструментов из объектов с методами <c>[AgentTool]</c>; строка метода считается удачей.</summary>
    /// <param name="instances">Объекты с методами-инструментами.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Набор контракта, как <see cref="AsToolSet(IEnumerable{object}, ToolTextResult, ILogger?)"/> с
    /// <see cref="ToolTextResult.Success"/>.</returns>
    [Obsolete("Строка метода здесь молча считается удачей. Выберите правило явно: перегрузка с ToolTextResult.")]
    public static IToolSet AsToolSet(IEnumerable<object> instances, ILogger? log = null) =>
        AsToolSet(instances, ToolTextResult.Success, log);

    /// <summary>Набор инструментов из объектов с методами <c>[AgentTool]</c>.</summary>
    /// <param name="instances">Объекты с методами-инструментами; порядок как у <c>ToolRegistry.FromObjects</c>: при
    /// совпадении имени побеждает последний.</param>
    /// <param name="textResults">Как понимать строку, которую вернул метод: удача или ошибка конфигурации.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Набор контракта. Метод без <see cref="ToolEffectAttribute"/> получает <see cref="ToolAction.Unknown"/> и
    /// <see cref="ToolIdempotency.Unknown"/> и одно предупреждение в журнал на инструмент.</returns>
    /// <exception cref="ArgumentException">У метода есть <see cref="ToolEffectAttribute"/>, но нет <c>[AgentTool]</c>: атрибут
    /// эффекта ничего бы не объявил, а пропускать его молча нельзя. При <see cref="ToolTextResult.Refused"/> — метод
    /// возвращает не итог с признаком успеха.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Правило не выбрано (<see cref="ToolTextResult.Unspecified"/>).</exception>
    public static IToolSet AsToolSet(IEnumerable<object> instances, ToolTextResult textResults, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(instances);
        object[] objects = [.. instances];

        var effects = new Dictionary<string, ToolEffect>(ToolSpec.NameComparer);

        foreach (object instance in objects)
        {
            ArgumentNullException.ThrowIfNull(instance, nameof(instances));
            EnsureNoOrphanEffects(instance);

            foreach ((string name, ToolRegistry.RegisteredTool tool) in ToolRegistry.ScanMethods(instance))
            {
                // Последний объект побеждает, как в реестре: прежнее объявление с тем же именем забывается.
                effects.Remove(name);

                if (tool.Method.GetCustomAttribute<ToolEffectAttribute>() is { } effect)
                    effects[name] = new ToolEffect(effect.Action, effect.Idempotency);
            }
        }

        var source = new ToolRegistryToolSource(
            ToolRegistry.FromObjects(objects), textResults, IsContractResult, ToOutcome);
        return new ReActSourceAsToolSet(source, baseRun: null, effects, log);
    }

    /// <summary>Итог контракта тоже несет признак успеха: правило <see cref="ToolTextResult.Refused"/> его принимает.</summary>
    private static bool IsContractResult(Type type) => type == typeof(ToolResult);

    /// <summary>Итог контракта едет в старой форме целиком; остальные значения — обычное правило источника.</summary>
    private static ReActToolOutcome? ToOutcome(object value) =>
        value is ToolResult result ? ReActTranslation.Carry(result) : null;

    /// <summary>Атрибут эффекта без <c>[AgentTool]</c> на том же методе — ошибка с именем типа и метода.</summary>
    private static void EnsureNoOrphanEffects(object instance)
    {
        MethodInfo[] methods = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        string[] orphans = [.. methods
            .Where(m => m.IsDefined(typeof(ToolEffectAttribute), inherit: false) && !m.IsDefined(typeof(AgentToolAttribute), inherit: false))
            .Select(m => instance.GetType().Name + "." + m.Name)];

        if (orphans.Length > 0)
        {
            throw new ArgumentException(
                $"[ToolEffect] без [AgentTool] на том же методе: {string.Join(", ", orphans)}. " +
                "Атрибут эффекта описывает инструмент, объявленный [AgentTool]; без него метод не инструмент.",
                nameof(instance));
        }
    }
}
