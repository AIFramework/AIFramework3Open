namespace AI.LLM.Agents.Planning;

/// <summary>
/// Скил (навык) — текстовая инструкция, описывающая как выполнять определённое действие.
/// Передаётся в LLM при генерации плана как дополнительный контекст.
/// <example>
/// <code>
/// new Skill("order_pizza",
///     "Для заказа пиццы перейди на сайт example.com, " +
///     "введи адрес из профиля, выбери пиццу и подтверди заказ.")
/// </code>
/// </example>
/// </summary>
public sealed class Skill
{
    /// <summary>Короткое имя скила (для идентификации).</summary>
    public string Name { get; }

    /// <summary>Полная текстовая инструкция.</summary>
    public string Description { get; }

    public Skill(string name, string description)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }
}
