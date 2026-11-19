using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Вид действия инструмента: что вызов делает с миром. По нему решают, нужно ли согласие человека
/// и можно ли повторить вызов.
/// </summary>
/// <remarks>
/// Ноль — <see cref="Unknown"/>, а не <see cref="Read"/>: незаданное свойство, пропущенное поле JSON и
/// неинициализированная запись не должны молча стать «чтением». Везде, где вид решает,
/// <see cref="Unknown"/> трактуется как <see cref="Irreversible"/>; трактовку дает один метод
/// <see cref="ToolActions.Effective(ToolAction)"/>, а не каждое место по-своему.
/// </remarks>
[JsonConverter(typeof(StrictEnumConverter<ToolAction>))]
public enum ToolAction
{
    /// <summary>Вид не объявлен. Трактуется как <see cref="Irreversible"/>.</summary>
    Unknown = 0,

    /// <summary>Только чтение: мир не меняется.</summary>
    Read,

    /// <summary>Вычисление без внешнего мира.</summary>
    Compute,

    /// <summary>Побочное действие, которое можно отменить: черновик, файл, пометка.</summary>
    Reversible,

    /// <summary>Действие, которое нельзя отменить: отправить, опубликовать, оплатить, удалить, дать согласие.</summary>
    Irreversible,
}
