using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Memory;

/// <summary>
/// Происхождение значения факта. Определяет его код по структуре хода, а не модель: модель только предлагает
/// факт и цитату.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<ValueSource>))]
public enum ValueSource
{
    /// <summary>Происхождение не задано.</summary>
    Unknown = 0,

    /// <summary>Сказано человеком; дословно — в <see cref="MemoryFact.Quote"/>.</summary>
    Said,

    /// <summary>
    /// Из документа или страницы. Содержимое источника — это RAG, поэтому такое значение в память не пишется,
    /// а отклоняется исходом с причиной; в памяти остается только отношение человека к документу.
    /// </summary>
    Document,

    /// <summary>Умолчание вида: типичное значение, унаследованное от <see cref="MemoryObject.Kind"/>.</summary>
    Kind,

    /// <summary>Выведено из других фактов; посылки — в <see cref="MemoryFact.Premises"/>.</summary>
    Derived,
}
