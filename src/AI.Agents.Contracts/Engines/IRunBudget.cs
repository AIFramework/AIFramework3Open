using AI.Agents.Contracts.Models;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Бюджет запуска: шаги, токены, деньги и срок. Отвечает движку, можно ли сделать еще шаг, и если нельзя —
/// почему, машинным кодом.
/// </summary>
/// <remarks>
/// Срок живет только здесь: у <see cref="EngineInput"/> отдельного поля срока нет. Плательщика в контракте
/// движка тоже нет: он приходит в реализацию <see cref="IModelPort"/>.
/// </remarks>
public interface IRunBudget
{
    /// <summary>Срок запуска; <see langword="null"/>: без срока.</summary>
    DateTimeOffset? Deadline { get; }

    /// <summary>
    /// Можно ли сделать еще шаг. Если можно, шаг засчитывается.
    /// </summary>
    /// <param name="stop">
    /// Если нельзя — код исчерпанного предела (<see cref="StopCode.StepLimit"/>, <see cref="StopCode.TokenLimit"/>,
    /// <see cref="StopCode.CostLimit"/>, <see cref="StopCode.TimeLimit"/> или свой); тот же код движок пишет в
    /// <see cref="EngineResult.StopCode"/>. Если можно — значение не задано.
    /// </param>
    /// <returns><see langword="true"/>, если шаг разрешен и засчитан.</returns>
    bool TryStep(out StopCode stop);

    /// <summary>
    /// Записать расход вызова модели: по нему бюджет следит за токенами и деньгами.
    /// </summary>
    /// <param name="usage">Расход одного вызова.</param>
    void Spend(ModelUsage usage);
}
