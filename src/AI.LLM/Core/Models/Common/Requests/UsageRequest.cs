using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Requests;

/// <summary>
/// Просьба к провайдеру вернуть блок <c>usage</c> с фактической стоимостью запроса
/// (<c>usage: {include: true}</c> у OpenRouter).
/// </summary>
/// <remarks>
/// Отдельный объект, а не флаг: в теле запроса это вложенная структура, и агрегаторы со временем
/// добавляют в неё поля. Ставится из <see cref="GenerateSettings.IncludeUsage"/>.
/// </remarks>
public class UsageRequest
{
    /// <summary>Возвращать ли расширенный блок расхода.</summary>
    [JsonPropertyName("include")]
    public bool Include { get; set; } = true;
}
