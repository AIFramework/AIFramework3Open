using System.Reflection;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents.ReAct.Interop;
using AI.LLM.Agents.Tools;
using Serilog;

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
/// Это единственный файл сборки, которому разрешено отражение (храповик <c>NoReflectionTests</c>, исключение с
/// причиной): чтение пользовательского атрибута с <see cref="MethodInfo"/> и поиск методов с
/// <see cref="ToolEffectAttribute"/> без <c>[AgentTool]</c>. Члены AI.LLM через отражение не вызываются.
/// </para>
/// </remarks>
public static class AttributedTools
{
    /// <summary>Набор инструментов из объектов с методами <c>[AgentTool]</c>.</summary>
    /// <param name="instances">Объекты с методами-инструментами; порядок как у <c>ToolRegistry.FromObjects</c>: при
    /// совпадении имени побеждает последний.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Набор контракта. Метод без <see cref="ToolEffectAttribute"/> получает <see cref="ToolAction.Unknown"/> и
    /// <see cref="ToolIdempotency.Unknown"/> и одно предупреждение в журнал на инструмент.</returns>
    /// <exception cref="ArgumentException">У метода есть <see cref="ToolEffectAttribute"/>, но нет <c>[AgentTool]</c>: атрибут
    /// эффекта ничего бы не объявил, а пропускать его молча нельзя.</exception>
    public static IToolSet AsToolSet(IEnumerable<object> instances, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(instances);
        object[] objects = [.. instances];

        var actions = new Dictionary<string, ToolAction>(ToolSpec.NameComparer);
        var idempotency = new Dictionary<string, ToolIdempotency>(ToolSpec.NameComparer);

        foreach (object instance in objects)
        {
            ArgumentNullException.ThrowIfNull(instance, nameof(instances));
            EnsureNoOrphanEffects(instance);

            foreach ((string name, ToolRegistry.RegisteredTool tool) in ToolRegistry.ScanMethods(instance))
            {
                // Последний объект побеждает, как в реестре: прежнее объявление с тем же именем забывается.
                actions.Remove(name);
                idempotency.Remove(name);

                if (tool.Method.GetCustomAttribute<ToolEffectAttribute>() is { } effect)
                {
                    actions[name] = effect.Action;
                    idempotency[name] = effect.Idempotency;
                }
            }
        }

        var source = new ToolRegistryToolSource(ToolRegistry.FromObjects(objects));
        return new ReActSourceAsToolSet(source, baseRun: null, actions, idempotency, log);
    }

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
