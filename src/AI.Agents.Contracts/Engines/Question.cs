using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Вопрос человеку. Один тип на уточнение данных и на согласие с действием.
/// </summary>
/// <remarks>
/// Вопрос может задать движок (<see cref="EngineResult.Question"/>) или инструмент
/// (<see cref="ToolResult.Question"/>). Запуск встает на паузу, ответ приходит структурой
/// <see cref="Answer"/>, и запуск продолжается с места. Вопрос дочернего запуска поднимается родителю как
/// есть: <see cref="AskedBy"/> указывает на ребенка.
/// </remarks>
public sealed record Question
{
    /// <summary>Идентификатор вопроса: на него ссылается <see cref="Answer.QuestionId"/>.</summary>
    public required string Id { get; init; }

    /// <summary>Вид вопроса.</summary>
    public required QuestionKind Kind { get; init; }

    /// <summary>Текст вопроса для человека.</summary>
    public required string Text { get; init; }

    /// <summary>Идентификатор запуска, который спросил: свой или дочерний.</summary>
    public required string AskedBy { get; init; }

    /// <summary>Поля для заполнения при <see cref="QuestionKind.Input"/>; <see langword="null"/>: полей нет.</summary>
    public IReadOnlyList<InputField>? Fields { get; init; }

    /// <summary>Для <see cref="QuestionKind.Confirm"/>: ключ действия, ждущего согласия (<see cref="ToolCall.EffectKey"/>); иначе <see langword="null"/>.</summary>
    public string? EffectKey { get; init; }

    /// <summary>Для <see cref="QuestionKind.Confirm"/>: вид этого действия; иначе <see langword="null"/>.</summary>
    public ToolAction? Action { get; init; }

    /// <summary>Для <see cref="QuestionKind.Confirm"/>: на что действие направлено — сайт, аккаунт, получатель; иначе <see langword="null"/>.</summary>
    public string? Target { get; init; }
}
