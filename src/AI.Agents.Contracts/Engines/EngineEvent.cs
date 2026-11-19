using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Событие прогона движка. Поток событий — единственный выход <see cref="IEngine.RunAsync(EngineInput, CancellationToken)"/>;
/// последним всегда идет <see cref="Finished"/> с итогом.
/// </summary>
/// <remarks>
/// События шага и инструмента несут <see cref="StepPath"/>. Продукт переводит события в свой протокол сам.
/// </remarks>
[JsonPolymorphic]
[JsonDerivedType(typeof(EngineEvent.Step), nameof(EngineEvent.Step))]
[JsonDerivedType(typeof(EngineEvent.ToolStarted), nameof(EngineEvent.ToolStarted))]
[JsonDerivedType(typeof(EngineEvent.ToolProgress), nameof(EngineEvent.ToolProgress))]
[JsonDerivedType(typeof(EngineEvent.ToolFinished), nameof(EngineEvent.ToolFinished))]
[JsonDerivedType(typeof(EngineEvent.Text), nameof(EngineEvent.Text))]
[JsonDerivedType(typeof(EngineEvent.Reasoning), nameof(EngineEvent.Reasoning))]
[JsonDerivedType(typeof(EngineEvent.Spent), nameof(EngineEvent.Spent))]
[JsonDerivedType(typeof(EngineEvent.Saved), nameof(EngineEvent.Saved))]
[JsonDerivedType(typeof(EngineEvent.Finished), nameof(EngineEvent.Finished))]
public abstract record EngineEvent
{
    /// <summary>Начат шаг.</summary>
    public sealed record Step : EngineEvent
    {
        /// <summary>Путь шага.</summary>
        public required StepPath Path { get; init; }

        /// <summary>Мысль модели на этом шаге; <see langword="null"/>: модель ее не написала.</summary>
        public string? Thought { get; init; }
    }

    /// <summary>Начат вызов инструмента.</summary>
    public sealed record ToolStarted : EngineEvent
    {
        /// <summary>Путь шага.</summary>
        public required StepPath Path { get; init; }

        /// <summary>Вызов.</summary>
        public required ToolCall Call { get; init; }
    }

    /// <summary>Прогресс вызова инструмента.</summary>
    public sealed record ToolProgress : EngineEvent
    {
        /// <summary>Путь шага.</summary>
        public required StepPath Path { get; init; }

        /// <summary>Вызов.</summary>
        public required ToolCall Call { get; init; }

        /// <summary>Кадр прогресса от инструмента.</summary>
        public required ToolUpdate.Progress Update { get; init; }
    }

    /// <summary>Вызов инструмента закончен.</summary>
    public sealed record ToolFinished : EngineEvent
    {
        /// <summary>Путь шага.</summary>
        public required StepPath Path { get; init; }

        /// <summary>Вызов.</summary>
        public required ToolCall Call { get; init; }

        /// <summary>Итог вызова.</summary>
        public required ToolResult Result { get; init; }
    }

    /// <summary>Фрагмент текста ответа.</summary>
    public sealed record Text : EngineEvent
    {
        /// <summary>Фрагмент.</summary>
        public required string Chunk { get; init; }
    }

    /// <summary>Фрагмент рассуждения модели. В ответ не входит.</summary>
    public sealed record Reasoning : EngineEvent
    {
        /// <summary>Фрагмент.</summary>
        public required string Chunk { get; init; }
    }

    /// <summary>Расход одного вызова модели.</summary>
    public sealed record Spent : EngineEvent
    {
        /// <summary>Расход.</summary>
        public required ModelUsage Usage { get; init; }
    }

    /// <summary>Граница сохранения: с этого места прогон можно продолжить.</summary>
    public sealed record Saved : EngineEvent
    {
        /// <summary>Путь шага, после которого сохранено состояние.</summary>
        public required StepPath Path { get; init; }

        /// <summary>Состояние движка для продолжения (<see cref="EngineInput.State"/>).</summary>
        public required JsonObject State { get; init; }
    }

    /// <summary>Итог прогона. Всегда последнее событие потока, ровно одно.</summary>
    public sealed record Finished : EngineEvent
    {
        /// <summary>Итог.</summary>
        public required EngineResult Result { get; init; }
    }
}
