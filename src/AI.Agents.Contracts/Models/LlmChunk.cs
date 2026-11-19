using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Кадр потокового ответа модели (<see cref="IModelPort.StreamAsync(ModelAsk, CancellationToken)"/>): фрагмент
/// текста (<see cref="Text"/>), фрагмент рассуждения (<see cref="Reasoning"/>) или итог (<see cref="Final"/>).
/// </summary>
/// <remarks>
/// Последний кадр потока всегда <see cref="Final"/>, и он один. Полный ответ, вызовы инструментов, статус и
/// расход лежат только в <see cref="Final.Answer"/>; фрагменты нужны для показа по мере написания, и
/// склеивать их, чтобы получить ответ, не нужно.
/// </remarks>
[JsonPolymorphic]
[JsonDerivedType(typeof(LlmChunk.Text), nameof(LlmChunk.Text))]
[JsonDerivedType(typeof(LlmChunk.Reasoning), nameof(LlmChunk.Reasoning))]
[JsonDerivedType(typeof(LlmChunk.Final), nameof(LlmChunk.Final))]
public abstract record LlmChunk
{
    /// <summary>Фрагмент текста ответа, только для показа.</summary>
    public sealed record Text : LlmChunk
    {
        /// <summary>Фрагмент.</summary>
        public required string Chunk { get; init; }
    }

    /// <summary>Фрагмент рассуждения модели, только для показа. В ответ не входит.</summary>
    public sealed record Reasoning : LlmChunk
    {
        /// <summary>Фрагмент.</summary>
        public required string Chunk { get; init; }
    }

    /// <summary>Итог вызова. Всегда последний кадр потока, ровно один.</summary>
    public sealed record Final : LlmChunk
    {
        /// <summary>Итог: полный текст, рассуждение, вызовы инструментов, статус, расход.</summary>
        public required LlmAnswer Answer { get; init; }
    }
}
