using System.Text.Json;

namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Описание инструмента: имя, описание, схема параметров и вид действия. Одно описание на все
/// транспорты: прямой вызов, нативный вызов инструментов модели, MCP.
/// </summary>
/// <remarks>
/// Имя проверяется при создании (<see cref="NamePattern"/>) и сравнивается с учетом регистра
/// (<see cref="NameComparer"/>): во фреймворке было четыре разных нормализации имени, здесь правило одно.
/// Схема параметров обязательна и должна быть объектом JSON Schema.
/// </remarks>
public sealed record ToolSpec
{
    /// <summary>Шаблон имени, как у OpenAI и MCP: от 1 до 64 знаков из латинских букв, цифр, «_» и «-».</summary>
    public const string NamePattern = "^[a-zA-Z0-9_-]{1,64}$";

    private const int MaxNameLength = 64;

    private readonly string _name = "";
    private readonly JsonElement _parameters;

    /// <summary>Сравнение имен инструментов: с учетом регистра, без культуры.</summary>
    public static StringComparer NameComparer => StringComparer.Ordinal;

    /// <summary>Имя инструмента; проверяется по <see cref="NamePattern"/>.</summary>
    /// <exception cref="ArgumentException">Имя не подходит под шаблон.</exception>
    public required string Name
    {
        get => _name;
        init => _name = IsValidName(value)
            ? value
            : throw new ArgumentException(
                $"Имя инструмента «{value}» не подходит: нужно от 1 до {MaxNameLength} знаков из латинских букв, " +
                "цифр, «_» и «-».",
                nameof(Name));
    }

    /// <summary>Описание одной строкой: его видит модель и клиент MCP.</summary>
    public required string Description { get; init; }

    /// <summary>JSON Schema параметров; корень — объект. Инструмент без параметров объявляет пустой объект.</summary>
    /// <exception cref="ArgumentException">Значение не объект JSON.</exception>
    public required JsonElement Parameters
    {
        get => _parameters;
        init => _parameters = value.ValueKind == JsonValueKind.Object
            ? value
            : throw new ArgumentException(
                $"Схема параметров инструмента должна быть объектом JSON Schema, а пришло {value.ValueKind}.",
                nameof(Parameters));
    }

    /// <summary>
    /// Объявленный вид действия. <see cref="ToolAction.Unknown"/> допустим (например, у инструмента чужого
    /// сервера без подсказок) и трактуется как <see cref="ToolAction.Irreversible"/>.
    /// </summary>
    public required ToolAction Action { get; init; }

    /// <summary>Группа каталога, по которой агент перечисляет инструменты; <see langword="null"/>: без группы.</summary>
    public string? Group { get; init; }

    /// <summary>Подробное описание по блокам; <see langword="null"/>: есть только <see cref="Description"/>.</summary>
    public About? About { get; init; }

    /// <summary>
    /// Подходит ли имя под <see cref="NamePattern"/>.
    /// </summary>
    /// <param name="name">Проверяемое имя.</param>
    /// <returns><see langword="true"/>, если имя можно дать инструменту.</returns>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            return false;

        foreach (char c in name)
        {
            bool allowed = c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-';

            if (!allowed)
                return false;
        }

        return true;
    }
}
