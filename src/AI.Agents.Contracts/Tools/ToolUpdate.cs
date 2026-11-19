using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Кадр потока вызова инструмента: прогресс (<see cref="Progress"/>) или итог (<see cref="Final"/>).
/// </summary>
/// <remarks>
/// <para>Правила потока <see cref="ITool.CallAsync(ToolCall, ToolContext, CancellationToken)"/>:</para>
/// <list type="bullet">
/// <item>Сколько угодно кадров <see cref="Progress"/>, затем ровно один <see cref="Final"/>.</item>
/// <item>
/// Два итога: окончателен первый <see cref="Final"/>. Вызывающий на нем перестает читать поток, поэтому
/// все после первого итога не исполняется и не учитывается.
/// </item>
/// <item>
/// Итога нет (поток кончился без <see cref="Final"/>): вызывающий сам составляет итог со статусом
/// <see cref="ToolStatus.Unknown"/> и кодом <c>no_result</c>. Исход неизвестен — действие могло состояться,
/// поэтому вызов сам не повторяется.
/// </item>
/// <item>
/// Тайм-аут: срок держит вызывающий; он прерывает поток и ставит <see cref="ToolStatus.Unknown"/> с кодом
/// <c>timeout</c>. Инструмент статус <see cref="ToolStatus.Unknown"/> не ставит.
/// </item>
/// </list>
/// </remarks>
[JsonPolymorphic]
[JsonDerivedType(typeof(ToolUpdate.Progress), nameof(ToolUpdate.Progress))]
[JsonDerivedType(typeof(ToolUpdate.Final), nameof(ToolUpdate.Final))]
public abstract record ToolUpdate
{
    /// <summary>Промежуточный прогресс вызова.</summary>
    public sealed record Progress : ToolUpdate
    {
        /// <summary>Что сейчас происходит, для человека и журнала.</summary>
        public required string Message { get; init; }

        /// <summary>Структурные данные прогресса; <see langword="null"/>: только <see cref="Message"/>.</summary>
        public JsonElement? Data { get; init; }
    }

    /// <summary>Итог вызова: последний кадр потока.</summary>
    public sealed record Final : ToolUpdate
    {
        /// <summary>Итог вызова.</summary>
        public required ToolResult Result { get; init; }
    }
}
