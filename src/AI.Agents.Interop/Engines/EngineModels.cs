using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Models;
using AI.LLM.Core.Abstractions;
using Serilog;

namespace AI.Agents.Interop.Engines;

/// <summary>
/// Какой ролью движки спрашивают порт модели — одно место для <c>react</c> и <c>plan</c>. Роль
/// <see cref="ModelRole.Decide"/> только у решения хода (шаг ReAct, план); служебные вызовы спрашивают порт своей
/// ролью: приемка шага — <see cref="ModelRole.Judge"/>.
/// </summary>
/// <remarks>
/// Новый служебный вызов движка получает здесь свою роль, а не берет клиент решения хода: иначе на
/// <c>ModelPort.Routed</c> дешевая проверка уходила бы на модель хода. Храповик <c>CallMechanicsRatchetTests</c>
/// (<c>engine-roles</c>) не пускает <c>ModelRole.</c> в другие файлы движков.
/// </remarks>
internal static class EngineModels
{
    /// <summary>Роль решения хода: шаг ReAct и план.</summary>
    public static ModelRole DecisionRole => ModelRole.Decide;

    /// <summary>Роль приемки шага.</summary>
    public static ModelRole AcceptanceRole => ModelRole.Judge;

    /// <summary>Клиент решения хода.</summary>
    public static ILLMClient Decision(EngineInput input, ILogger? log) => input.Model.AsLlmClient(DecisionRole, log);

    /// <summary>Что умеет модель решения хода.</summary>
    public static ModelCapabilities DecisionCapabilities(EngineInput input) => input.Model.Describe(DecisionRole);

    /// <summary>Роли, которые нужны запуску: решение хода всегда, остальные — по параметрам движка.</summary>
    /// <param name="options">Разобранные параметры движка.</param>
    public static IEnumerable<ModelRole> RequiredRoles(EngineOptions options)
    {
        yield return DecisionRole;
        if (options.Acceptance == EngineOptions.Model)
            yield return AcceptanceRole;
    }

    /// <summary>
    /// Первая роль запуска, которую порт не настраивает; <see langword="null"/>: все на месте. Порт спрашивается
    /// <see cref="IModelPort.Describe(ModelRole)"/>, без вызова модели: ненастроенная роль — <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="input">Вход движка.</param>
    /// <param name="options">Разобранные параметры движка.</param>
    public static ModelRole? MissingRole(EngineInput input, EngineOptions options)
    {
        foreach (ModelRole role in RequiredRoles(options))
        {
            try
            {
                input.Model.Describe(role);
            }
            catch (ArgumentException)
            {
                return role;
            }
        }

        return null;
    }

    /// <summary>Клиент приемки шага.</summary>
    public static ILLMClient Acceptance(EngineInput input, ILogger? log) => input.Model.AsLlmClient(AcceptanceRole, log);
}
