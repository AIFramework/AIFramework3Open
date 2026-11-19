using System.Text.Json;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Ответ человека на <see cref="Question"/>: значения по ключам полей, а не текст реплики.
/// </summary>
public sealed record Answer
{
    /// <summary>На какой вопрос ответ (<see cref="Question.Id"/>).</summary>
    public required string QuestionId { get; init; }

    /// <summary>Значения по ключам полей (<see cref="InputField.Key"/>); <see langword="null"/>: полей не было.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Values { get; init; }

    /// <summary>
    /// Согласие на действие при <see cref="QuestionKind.Confirm"/>: <see langword="true"/>: да,
    /// <see langword="false"/>: нет; <see langword="null"/>: вопрос был не о согласии.
    /// </summary>
    public bool? Confirmed { get; init; }
}
