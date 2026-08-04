using System.Text.Json.Serialization;

namespace AI.LLM.Agents.Planning;

/// <summary>
/// Один шаг плана: описание действия, привязка к инструменту и зависимости.
/// </summary>
public sealed class PlanStep
{
    /// <summary>Уникальный идентификатор шага (например, "step_0").</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>Человекочитаемое описание действия.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// Критерий готовности: по чему видно, что шаг СДЕЛАН, а не начат.
    /// </summary>
    /// <remarks>
    /// Проверяемый признак результата, а не пересказ задачи: «в ответе есть готовый текст
    /// эссе не меньше 2000 знаков», а не «эссе написано». Нужен приёмке результата —
    /// без него «выполнено» означает лишь то, что исполнитель отработал, и план шёл дальше
    /// с планом работы вместо самой работы. Пусто — принимать по описанию шага.
    /// </remarks>
    [JsonPropertyName("done_when")]
    public string DoneWhen { get; set; }

    /// <summary>Имя инструмента [AgentTool] для выполнения (null — ручной шаг).</summary>
    [JsonPropertyName("tool")]
    public string ToolName { get; set; }

    /// <summary>Аргументы для вызова инструмента.</summary>
    [JsonPropertyName("args")]
    public Dictionary<string, string> ToolArguments { get; set; } = [];

    /// <summary>Идентификаторы шагов-предшественников (зависимости).</summary>
    [JsonPropertyName("depends_on")]
    public List<string> DependsOn { get; set; } = [];

    /// <summary>
    /// Выходы шага: имя порта инструмента -> идентификатор артефакта, под которым исполнитель
    /// сохранит результат.
    /// </summary>
    /// <remarks>
    /// Нужны, чтобы следующий шаг мог сослаться на КОНКРЕТНЫЙ результат, а не на «всё, что было
    /// раньше». Пустой словарь — шаг ничего не передаёт дальше.
    /// </remarks>
    [JsonPropertyName("outputs")]
    public Dictionary<string, string> Outputs { get; set; } = [];

    /// <summary>
    /// Входы шага: имя порта инструмента -> источник данных
    /// (<c>step_X.outputs.port_Y</c> либо <c>user_context.ключ</c>).
    /// </summary>
    /// <remarks>
    /// Это и есть рёбра графа данных. Совместимость портов источника и приёмника проверяет
    /// вызывающая сторона (у библиотеки нет знания об онтологии конкретного приложения),
    /// поэтому здесь маппинг хранится как есть — строками.
    /// </remarks>
    [JsonPropertyName("input_mapping")]
    public Dictionary<string, string> InputMapping { get; set; } = [];

    /// <summary>Ярус (уровень параллелизма), вычисляемый алгоритмом Кана.</summary>
    [JsonIgnore]
    public int Tier { get; internal set; }
}
