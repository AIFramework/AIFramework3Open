using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Машинный код причины остановки движка. Один словарь для <see cref="IRunBudget.TryStep(out StopCode)"/> и
/// <see cref="EngineResult.StopCode"/>: бюджет называет исчерпанный предел, и тот же код уходит в итог.
/// </summary>
/// <remarks>
/// Набор открытый: движок со своей причиной создает код сам (<c>new StopCode("my_reason")</c>). Известные коды
/// покрывают пределы бюджета и причины остановки цикла <c>ReActEngine</c>. Сравнение по <see cref="Name"/> с
/// учетом регистра.
/// </remarks>
public readonly record struct StopCode
{
    /// <summary>
    /// Создает код с именем.
    /// </summary>
    /// <param name="name">Имя кода, например <c>step_limit</c>; пустое не допускается.</param>
    /// <exception cref="ArgumentException">Имя пустое или из пробелов.</exception>
    [JsonConstructor]
    public StopCode(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Имя кода.</summary>
    public string Name { get; }

    /// <summary>Модель дала окончательный ответ.</summary>
    public static StopCode FinalAnswer { get; } = new("final_answer");

    /// <summary>Инструмент-финал завершил запуск (<see cref="Tools.ToolResult.EndsRun"/>).</summary>
    public static StopCode TerminalTool { get; } = new("terminal_tool");

    /// <summary>Запуск стоит на вопросе человеку.</summary>
    public static StopCode AwaitsUser { get; } = new("awaits_user");

    /// <summary>Исчерпан предел шагов.</summary>
    public static StopCode StepLimit { get; } = new("step_limit");

    /// <summary>Исчерпан предел токенов.</summary>
    public static StopCode TokenLimit { get; } = new("token_limit");

    /// <summary>Исчерпан предел денег.</summary>
    public static StopCode CostLimit { get; } = new("cost_limit");

    /// <summary>Исчерпан предел вызовов модели.</summary>
    public static StopCode CallLimit { get; } = new("call_limit");

    /// <summary>Вышел срок.</summary>
    public static StopCode TimeLimit { get; } = new("time_limit");

    /// <summary>Шаги повторяются без продвижения.</summary>
    public static StopCode NoProgress { get; } = new("no_progress");

    /// <summary>Задаче нужны инструменты, а доступных нет.</summary>
    public static StopCode NoTools { get; } = new("no_tools");

    /// <summary>Политика решения шага не смогла получить от модели пригодное решение.</summary>
    public static StopCode PolicyFailure { get; } = new("policy_failure");

    /// <summary>Сбой движка.</summary>
    public static StopCode EngineFailure { get; } = new("engine_failure");

    /// <summary>
    /// Сбой хоста модели: вызов не удался после повторов и запасных моделей (ключ, баланс, окно, сеть, провайдер).
    /// Код сбоя модели (<see cref="Models.LlmFailure"/>) и причина — в <see cref="EngineResult.Unfinished"/>.
    /// </summary>
    public static StopCode ModelFailure { get; } = new("model_failure");

    /// <summary>Запуск отменен.</summary>
    public static StopCode Cancelled { get; } = new("cancelled");

    /// <summary>Имя кода.</summary>
    /// <returns><see cref="Name"/>.</returns>
    public override string ToString() => Name ?? "";
}
