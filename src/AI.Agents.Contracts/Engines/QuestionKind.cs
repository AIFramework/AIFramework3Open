using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Зачем запуск спрашивает человека.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<QuestionKind>))]
public enum QuestionKind
{
    /// <summary>Вид не задан.</summary>
    Unknown = 0,

    /// <summary>Не хватает данных: человек заполняет поля <see cref="Question.Fields"/>.</summary>
    Input,

    /// <summary>Нужно согласие на действие: человек отвечает <see cref="Answer.Confirmed"/>.</summary>
    Confirm,
}
