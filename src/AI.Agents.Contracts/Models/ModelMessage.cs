using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Сообщение разговора с моделью: части с происхождением, вызовы инструментов со стороны модели и
/// результат инструмента с идентификатором вызова.
/// </summary>
public sealed record ModelMessage
{
    /// <summary>Автор сообщения.</summary>
    public required MessageRole Role { get; init; }

    private readonly IReadOnlyList<MessagePart>? _parts;

    /// <summary>Части: текст, картинки, звук; <see langword="null"/>: частей нет (например, у сообщения только с вызовами).</summary>
    /// <exception cref="ArgumentException">Часть равна null или у звука нет ни байтов, ни адреса.</exception>
    public IReadOnlyList<MessagePart>? Parts
    {
        get => _parts;
        init
        {
            if (value is not null)
            {
                for (int i = 0; i < value.Count; i++)
                {
                    string name = $"{nameof(Parts)}[{i}]";

                    if (value[i] is null)
                        throw new ArgumentException("Часть сообщения не может быть null.", name);

                    if (value[i] is MessagePart.Audio audio)
                        MessagePart.Audio.EnsureOne(audio, name);
                }
            }

            _parts = value;
        }
    }

    /// <summary>Вызовы инструментов, которые сделала модель (<see cref="MessageRole.Assistant"/>); <see langword="null"/>: вызовов нет.</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>На какой вызов это результат (<see cref="MessageRole.Tool"/>, <see cref="ToolCall.Id"/>); иначе <see langword="null"/>.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// Метка кэша: разговор до этого сообщения включительно провайдер может закэшировать;
    /// <see langword="false"/>: метки нет.
    /// </summary>
    public bool Cache { get; init; }
}
