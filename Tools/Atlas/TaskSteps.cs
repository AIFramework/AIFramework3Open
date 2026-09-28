using AI.LLM.Agents.Planning;
using AI.LLM.Agents.Tools;
using AI.LLM.Core.Abstractions;
using System.Text.RegularExpressions;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Разбиение задачи на шаги: моделью через планировщик <c>AI.LLM</c> либо по тексту.
/// </summary>
/// <remarks>
/// В режиме MCP шаги обычно передаёт сам клиент: модель, которая пишет код, разбивает задачу
/// лучше любого правила. Разбор по тексту — запасной путь без ключа: он режет по переносам,
/// точкам с запятой, стрелкам, нумерации и связкам «затем», «потом», «после этого».
/// </remarks>
public static class TaskSteps
{
    private const int MaxSteps = 6;

    private static readonly Regex Separators = new(
        @"\r?\n|;|→|->|\s\d+[.)]\s|,?\s+(?:а\s+)?(?:затем|потом|после\s+этого|после\s+чего)\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Шаги по тексту задачи; нечего резать — один шаг, вся задача.</summary>
    public static IReadOnlyList<string> Split(string task)
    {
        ArgumentNullException.ThrowIfNull(task);

        string[] parts = [.. Separators.Split(" " + task.Trim())
            .Select(part => Regex.Replace(part, @"^\s*(?:\d+[.)]|[-*•])\s*", "").Trim(' ', '.', ','))
            .Where(part => part.Length > 2)];

        return parts.Length == 0 ? [task.Trim()] : [.. parts.Take(MaxSteps)];
    }

    /// <summary>
    /// Шаги от модели через <see cref="PlanGeneratorBuilder"/>: план без инструментов, шаги в
    /// порядке ярусов топологической сортировки.
    /// </summary>
    public static async Task<IReadOnlyList<string>> PlanAsync(ILLMClient llm, string task, CancellationToken cancellation = default)
    {
        PlanGenerator planner = PlanGeneratorBuilder.Create()
            .WithLLM(llm)
            .WithMaxSteps(MaxSteps)
            .WithTemperature(0)
            .Build();

        PlanTree plan = await planner.GenerateAsync(
            "Разбей задачу разработчика на шаги обработки данных, каждый шаг — одно действие, " +
            "которое может сделать одна функция библиотеки. Задача: " + task,
            additionalSkills: null, toolsOverride: new ToolRegistry(), ct: cancellation);

        return [.. plan.Tiers.SelectMany(tier => tier.Steps).Select(step => step.Description).Where(text => !string.IsNullOrWhiteSpace(text))];
    }
}
