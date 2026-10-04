using System.Text;
using AI.LLM.Agents.Tools;

namespace AI.LLM.Agents.Planning;

/// <summary>
/// Инструменты планирования для ReAct-агента: <c>plan</c> (первый вызов) и <c>replan</c>
/// (перепланирование при провале). Оба используют <see cref="PlanGenerator"/> со скилами
/// и ведут <see cref="PlanTracker"/>.
/// </summary>
public sealed class PlanTool
{
    /// <summary>Имя инструмента первичного планирования.</summary>
    public const string PlanToolName = "plan";

    /// <summary>Имя инструмента перепланирования.</summary>
    public const string ReplanToolName = "replan";

    private readonly PlanGenerator _generator;

    private string _currentGoal = "";

    /// <summary>Трекер прогресса текущего плана. Null до первого вызова plan().</summary>
    public PlanTracker Tracker { get; private set; }

    /// <summary>Создаёт инструменты планирования поверх готового генератора.</summary>
    public PlanTool(PlanGenerator generator) => _generator = generator;

    /// <summary>Строит план для цели и запускает трекер.</summary>
    [AgentTool(PlanToolName,
        "Generate a step-by-step execution plan for the goal. " +
        "MUST be called first, before any other tool.")]
    public async Task<string> Plan(
        [ToolParameter("The goal to plan for")] string goal,
        CancellationToken cancellationToken = default)
    {
        _currentGoal = goal;
        Console.WriteLine($"\n[Plan] Generating plan for: {goal}");

        var tree = await _generator.GenerateAsync(goal, null, cancellationToken);
        return ApplyNewPlan(tree, priorFailure: null);
    }

    /// <summary>Перестраивает план текущей цели с учётом причины провала.</summary>
    [AgentTool(ReplanToolName,
        "Regenerate the plan when the current approach is not working after retries. " +
        "Describe what failed so the new plan avoids the same mistake.")]
    public async Task<string> Replan(
        [ToolParameter("What went wrong and why the current plan failed")] string reason,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n[Replan] Reason: {reason}");

        var failureSkill = new Skill("previous_failure",
            $"Previous attempt failed: {reason}. " +
            "Generate a different plan that avoids the same mistake. " +
            "Try alternative tools or a different sequence of actions.");

        var tree = await _generator.GenerateAsync(
            _currentGoal, [failureSkill], cancellationToken);

        return ApplyNewPlan(tree, priorFailure: reason);
    }

    private string ApplyNewPlan(PlanTree tree, string priorFailure)
    {
        if (tree.Steps.Count == 0)
        {
            Console.WriteLine("[Plan] Could not generate a structured plan.");
            Tracker = null;
            return "Could not generate a structured plan. " +
                   "Proceed step by step: observe the current state first.";
        }

        if (Tracker != null)
            Tracker.Reset(tree.Steps);
        else
            Tracker = new PlanTracker(tree.Steps);

        var formatted = FormatPlan(tree, priorFailure);
        Console.WriteLine(formatted);
        return formatted;
    }

    private static string FormatPlan(PlanTree tree, string priorFailure)
    {
        var sb = new StringBuilder();

        if (priorFailure != null)
            sb.AppendLine($"*** Replanned after failure: {priorFailure} ***");

        sb.AppendLine($"Plan for: {tree.Goal}");
        sb.AppendLine();

        foreach (var tier in tree.Tiers)
        {
            foreach (var step in tier.Steps)
            {
                var tool = step.ToolName != null ? $" [{step.ToolName}]" : "";
                var deps = step.DependsOn.Count > 0
                    ? $" (after: {string.Join(", ", step.DependsOn)})"
                    : "";
                sb.AppendLine($"  {step.Id}{tool}: {step.Description}{deps}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Execute ONE tool per turn. " +
                      $"If a step fails {PlanTracker.MaxRetries} times, call {ReplanToolName}(reason=\"...\").");
        return sb.ToString();
    }
}
