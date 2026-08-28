namespace AI.Script.Runtime;

/// <summary>
/// Израсходованное прогоном на платные внешние вызовы.
/// </summary>
/// <remarks>
/// Отдельный тип, а не три свойства контекста: расход всегда читают целиком — вызовы без
/// токенов и токены без стоимости не отвечают ни на один вопрос.
/// </remarks>
/// <param name="Calls">Сколько платных вызовов сделано.</param>
/// <param name="Tokens">Сколько токенов израсходовано.</param>
/// <param name="Cost">Сколько потрачено в единицах биллинга.</param>
public readonly record struct ExternalUsage(int Calls, long Tokens, decimal Cost);
