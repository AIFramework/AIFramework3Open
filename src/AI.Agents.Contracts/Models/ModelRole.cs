using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Роль модели в запросе: зачем она нужна, а не как называется. Модель на роль выбирает реализация
/// <see cref="IModelPort"/>, движок имени модели не знает.
/// </summary>
/// <remarks>
/// Набор открытый: своя роль создается конструктором (<c>new ModelRole("extract")</c>). Сравнение по
/// <see cref="Name"/> с учетом регистра.
/// </remarks>
public readonly record struct ModelRole
{
    /// <summary>
    /// Создает роль с именем.
    /// </summary>
    /// <param name="name">Имя роли; пустое не допускается.</param>
    /// <exception cref="ArgumentException">Имя пустое или из пробелов.</exception>
    [JsonConstructor]
    public ModelRole(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Имя роли.</summary>
    public string Name { get; }

    /// <summary>Решить следующий шаг: подумать, выбрать инструмент.</summary>
    public static ModelRole Decide { get; } = new("decide");

    /// <summary>Написать итоговый текст.</summary>
    public static ModelRole Write { get; } = new("write");

    /// <summary>Оценить результат: проверка, приемка.</summary>
    public static ModelRole Judge { get; } = new("judge");

    /// <summary>Понять изображение.</summary>
    public static ModelRole See { get; } = new("see");

    /// <summary>Имя роли.</summary>
    /// <returns><see cref="Name"/>.</returns>
    public override string ToString() => Name ?? "";
}
