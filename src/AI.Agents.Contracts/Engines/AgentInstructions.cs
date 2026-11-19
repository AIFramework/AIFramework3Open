using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Инструкции агента: ключ промпта (<see cref="Key"/>) у хоста или текст (<see cref="Text"/>), ровно одно из двух.
/// </summary>
/// <remarks>
/// Правило «ровно одно» проверяется при создании, а не отдельным методом: запись, которую нельзя исполнить, не
/// должна дожить до хоста. Второе значение рядом с первым отвергается сразу, в каком бы порядке их ни задали.
/// Отсутствие обоих отвергается при чтении JSON и при записи в <see cref="AgentDefinition.Instructions"/>.
/// Создавать удобнее через <see cref="FromKey(string)"/> и <see cref="FromText(string)"/>.
/// </remarks>
public sealed record AgentInstructions : IJsonOnDeserialized
{
    private const string NoneMessage =
        "Инструкции агента: нужен ключ промпта (Key) или текст (Text), ровно одно из двух, а не задано ни одного.";

    private readonly string? _key;
    private readonly string? _text;

    /// <summary>Ключ промпта в хранилище хоста; <see langword="null"/>: инструкции заданы <see cref="Text"/>.</summary>
    /// <exception cref="ArgumentException">Строка пустая или уже задан <see cref="Text"/>.</exception>
    public string? Key
    {
        get => _key;
        init => _key = Checked(value, _text, nameof(Key));
    }

    /// <summary>Текст инструкций; <see langword="null"/>: инструкции по ключу <see cref="Key"/>.</summary>
    /// <exception cref="ArgumentException">Строка пустая или уже задан <see cref="Key"/>.</exception>
    public string? Text
    {
        get => _text;
        init => _text = Checked(value, _key, nameof(Text));
    }

    /// <summary>
    /// Инструкции по ключу промпта.
    /// </summary>
    /// <param name="key">Ключ; пустой не допускается.</param>
    /// <returns>Инструкции с заданным <see cref="Key"/>.</returns>
    /// <exception cref="ArgumentException">Ключ пустой или из пробелов.</exception>
    public static AgentInstructions FromKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new AgentInstructions { Key = key };
    }

    /// <summary>
    /// Инструкции текстом.
    /// </summary>
    /// <param name="text">Текст; пустой не допускается.</param>
    /// <returns>Инструкции с заданным <see cref="Text"/>.</returns>
    /// <exception cref="ArgumentException">Текст пустой или из пробелов.</exception>
    public static AgentInstructions FromText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new AgentInstructions { Text = text };
    }

    /// <inheritdoc />
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (_key is null && _text is null)
            throw new JsonException(NoneMessage);
    }

    /// <summary>Проверка, что задано хотя бы одно из двух; оба сразу исключены уже при создании.</summary>
    internal static void EnsureOne(AgentInstructions instructions, string paramName)
    {
        if (instructions.Key is null && instructions.Text is null)
            throw new ArgumentException(NoneMessage, paramName);
    }

    private static string? Checked(string? value, string? other, string name)
    {
        if (value is null)
            return null;

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Инструкции агента: {name} не может быть пустым; без значения — null.", name);

        if (other is not null)
        {
            throw new ArgumentException(
                "Инструкции агента: заданы и ключ промпта (Key), и текст (Text), а нужно ровно одно из двух.", name);
        }

        return value;
    }
}
