using System.Text.Json.Nodes;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Выбор движка в записи агента (<see cref="AgentDefinition.Engine"/>): ключ и настройки, которые понимает
/// только сам движок.
/// </summary>
public sealed record EngineChoice
{
    private readonly string _key = "";

    /// <summary>Ключ движка (<see cref="IEngine.Key"/>), например <c>react</c>; неизвестный ключ — отказ хоста.</summary>
    /// <exception cref="ArgumentException">Строка пустая или из пробелов.</exception>
    public required string Key
    {
        get => _key;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Key));
            _key = value;
        }
    }

    /// <summary>Настройки движка; <see langword="null"/>: умолчания самого движка.</summary>
    public JsonObject? Options { get; init; }
}
