namespace AI.Agents.Contracts.Memory;

/// <summary>След отбора: почему факт попал в срез памяти. Идет в журнал хода.</summary>
public sealed record MemoryPick
{
    /// <summary>Идентификатор выбранного факта (<see cref="MemoryFact.Id"/>).</summary>
    public required string Fact { get; init; }

    /// <summary>Канал отбора: одно из <see cref="Channels"/> или свое имя реализации.</summary>
    public required string Channel { get; init; }

    /// <summary>Оценка канала (больше — ближе к задаче); <see langword="null"/>: канал не оценивает (профиль).</summary>
    public double? Score { get; init; }

    /// <summary>Известные каналы отбора. Набор открытый: реализация может назвать свой канал.</summary>
    public static class Channels
    {
        /// <summary>Профиль: попадает всегда.</summary>
        public const string Profile = "profile";

        /// <summary>Совпадение слов задачи.</summary>
        public const string Words = "words";

        /// <summary>Близость по смыслу.</summary>
        public const string Meaning = "meaning";

        /// <summary>Соседство в графе с уже выбранным объектом.</summary>
        public const string Graph = "graph";

        /// <summary>Посылка выбранного вывода.</summary>
        public const string Premise = "premise";
    }
}
