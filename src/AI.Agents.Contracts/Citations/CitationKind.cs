using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Citations;

/// <summary>
/// Вид опоры, на которой держится утверждение.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<CitationKind>))]
public enum CitationKind
{
    /// <summary>Вид не задан.</summary>
    Unknown = 0,

    /// <summary>Документ или файл; <see cref="Citation.Url"/> указывает на место внутри него.</summary>
    File,

    /// <summary>Веб-страница; <see cref="Citation.Site"/>: сама страница, <see cref="Citation.Url"/>: ее сохраненная копия, если есть.</summary>
    Web,

    /// <summary>Вывод инструмента; <see cref="Citation.Title"/>: имя инструмента, <see cref="Citation.Step"/>: шаг вызова.</summary>
    Tool,

    /// <summary>Расчет; <see cref="Citation.Quote"/>: формула, <see cref="Citation.Basis"/>: исходные величины со своими опорами.</summary>
    Calculation,

    /// <summary>Запись памяти агента или графа знаний.</summary>
    Memory,

    /// <summary>Внешний сервис; <see cref="Citation.Title"/>: сервис, <see cref="Citation.Taken"/>: момент получения данных.</summary>
    Integration,
}
