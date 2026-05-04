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

    /// <summary>Имя инструмента [AgentTool] для выполнения (null — ручной шаг).</summary>
    [JsonPropertyName("tool")]
    public string ToolName { get; set; }

    /// <summary>Аргументы для вызова инструмента.</summary>
    [JsonPropertyName("args")]
    public Dictionary<string, string> ToolArguments { get; set; } = [];

    /// <summary>Идентификаторы шагов-предшественников (зависимости).</summary>
    [JsonPropertyName("depends_on")]
    public List<string> DependsOn { get; set; } = [];

    /// <summary>Ярус (уровень параллелизма), вычисляемый алгоритмом Кана.</summary>
    [JsonIgnore]
    public int Tier { get; internal set; }
}
