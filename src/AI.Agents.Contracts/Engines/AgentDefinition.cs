using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Запись агента: кто он, чем пользуется, как думает и что отдает. Агент — это данные, а не класс; исполняет
/// запись хост, выбирая движок (<see cref="IEngine"/>) по <see cref="Engine"/>.
/// </summary>
/// <remarks>
/// <para>
/// В запись не входят исполнитель хода, состояние и хранилище запусков, публикация (цена, видимость, витрина) и
/// рамки хоста (тариф, плательщик, разрешенные движки). Это свойства конкретного хоста и конкретного запуска, а
/// не агента: одна и та же запись исполняется у разных хостов с разными рамками.
/// </para>
/// <para>
/// Правка записи дает новую <see cref="Version"/>, а запуск фиксирует ту версию, на которой начат
/// (<see cref="ToRef"/>).
/// </para>
/// </remarks>
public sealed record AgentDefinition
{
    private readonly int _version;
    private readonly AgentInstructions _instructions = null!;

    /// <summary>Постоянный идентификатор агента (непрозрачная строка хоста); не меняется между версиями.</summary>
    public required string Id { get; init; }

    /// <summary>Версия записи, с 1; каждая правка дает следующую.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Версия меньше 1.</exception>
    public required int Version
    {
        get => _version;
        init => _version = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Version), value, "Версия записи агента считается с 1.");
    }

    /// <summary>Имя для показа.</summary>
    public required string Name { get; init; }

    /// <summary>Кто это, одной фразой: «аналитик по договорам поставки».</summary>
    public required string Role { get; init; }

    /// <summary>
    /// Подробное описание по блокам, тот же тип, что у <see cref="ToolSpec.About"/>: по нему агента выбирают
    /// исполнителем задачи; <see langword="null"/>: есть только <see cref="Role"/>.
    /// </summary>
    public About? About { get; init; }

    /// <summary>
    /// Имена инструментов (<see cref="ToolSpec.Name"/>) и групп (<see cref="ToolSpec.Group"/>), доступных агенту;
    /// <see langword="null"/>: агент работает без инструментов.
    /// </summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>
    /// Выбор движка: ключ (<see cref="IEngine.Key"/>) и его настройки; <see langword="null"/>: движок по
    /// умолчанию у хоста.
    /// </summary>
    /// <remarks>
    /// Неизвестный хосту ключ — явный отказ хоста запустить агента, без подмены другим движком: агент,
    /// рассчитанный на один способ думать, молча исполненный другим, дал бы непредсказуемый итог.
    /// </remarks>
    public EngineChoice? Engine { get; init; }

    /// <summary>
    /// Роль модели (<see cref="ModelRole"/>), а не имя модели: модель на роль выбирает реализация
    /// <see cref="IModelPort"/>; <see langword="null"/>: роли по шагам выбирает движок.
    /// </summary>
    public ModelRole? Model { get; init; }

    /// <summary>
    /// Собственные потолки агента; <see langword="null"/>: своих потолков нет, действуют рамки хоста. Хост
    /// строит <see cref="IRunBudget"/> из своих рамок и этих потолков, побеждает меньший.
    /// </summary>
    public AgentLimits? Limits { get; init; }

    /// <summary>Инструкции агента: ключ промпта или текст, ровно одно из двух.</summary>
    /// <exception cref="ArgumentException">Не задано ни ключа, ни текста.</exception>
    public required AgentInstructions Instructions
    {
        get => _instructions;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Instructions));
            AgentInstructions.EnsureOne(value, nameof(Instructions));
            _instructions = value;
        }
    }

    /// <summary>Что агент спрашивает у человека перед началом; <see langword="null"/>: ничего не спрашивает.</summary>
    public IReadOnlyList<InputField>? Inputs { get; init; }

    /// <summary>Кого агент может позвать дочерним запуском; <see langword="null"/>: никого.</summary>
    public IReadOnlyList<AgentRef>? Agents { get; init; }

    /// <summary>
    /// Виды итога, которые агент отдает (например, <c>text</c>, <c>table</c>, <c>docx</c>), набор открытый;
    /// <see langword="null"/>: виды не объявлены.
    /// </summary>
    public IReadOnlyList<string>? Output { get; init; }

    /// <summary>Ключ набора проверок приемки итога у хоста; <see langword="null"/>: приемки нет.</summary>
    public string? Acceptance { get; init; }

    /// <summary>
    /// Ссылка на эту версию записи.
    /// </summary>
    /// <returns><see cref="AgentRef"/> с <see cref="Id"/> и <see cref="Version"/>.</returns>
    public AgentRef ToRef() => new() { Id = Id, Version = Version };
}
