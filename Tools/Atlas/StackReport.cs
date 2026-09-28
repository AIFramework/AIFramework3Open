using AI.Script.Charts;
using AI.Script.Chem;
using AI.Script.Hosting;
using AI.Script.Llm;
using AI.Script.Nn;
using AI.Script.Std;
using AI.Script.Vision;
using System.Text;

namespace AiFramework.Tools.Atlas;

/// <summary>Ответ на «чем собрать задачу»: шаги, стеки, проверка лучшего.</summary>
public sealed record StackAnswer(
    IReadOnlyList<string> Steps, string StepsSource, IReadOnlyList<StackPlan> Plans, CheckOutcome? Check, string Dependencies);

/// <summary>
/// Сборка стека под задачу целиком: шаги, стеки, проверка, зависимости, текст отчёта.
/// </summary>
public static class StackReport
{
    /// <summary>
    /// Хост AIScript с теми же модулями, что у утилиты <c>aisc</c> и у MCP-сервера: проверка
    /// стека должна видеть те же функции, что и справка <c>script_help</c>.
    /// </summary>
    public static ScriptHost CreateScriptHost() =>
        StandardLibrary.CreateHost().UseCharts().UseLlm().UseChem().UseNeuralNetworks().UseVision();

    /// <summary>Собирает и проверяет стек.</summary>
    /// <param name="context">Индекс и поиск.</param>
    /// <param name="task">Задача словами.</param>
    /// <param name="steps">Шаги от клиента; <c>null</c> — разбить моделью или по тексту.</param>
    /// <param name="script">Искать только функции AIScript и проверять скриптом.</param>
    public static async Task<StackAnswer> BuildAsync(
        AtlasContext context, string task, IReadOnlyList<string>? steps, bool script, CancellationToken cancellation = default) =>
        await BuildAsync(context, context.Stacks(script), task, steps, script, cancellation);

    /// <summary>Собирает и проверяет стек заданным сборщиком: для подбора порогов на бенчмарке.</summary>
    public static async Task<StackAnswer> BuildAsync(
        AtlasContext context, StackBuilder builder, string task, IReadOnlyList<string>? steps, bool script, CancellationToken cancellation = default)
    {
        string source;

        if (steps is { Count: > 0 }) source = "шаги переданы";
        else if (context.LlmClient is { } llm)
        {
            steps = await TaskSteps.PlanAsync(llm, task, cancellation);
            source = $"шаги от {context.Settings.SearchModel}";
        }
        else
        {
            steps = TaskSteps.Split(task);
            source = "шаги по тексту задачи (нет ключа или searchModel)";
        }

        IReadOnlyList<StackPlan> plans = await builder.BuildAsync(steps, cancellation: cancellation);

        if (plans.Count == 0) return new StackAnswer(steps, source, plans, null, "");

        StackPlan best = plans[0];
        CheckOutcome check = script ? StackCheck.CheckScript(best, CreateScriptHost()) : StackCheck.CompileCSharp(best, context.Graph, context.Root);
        string dependencies = ProjectDeps.Describe(context.Root, best.Steps.Where(step => step.Hit != null).Select(step => step.Hit!.Unit.Project));

        return new StackAnswer(steps, source, plans, check, dependencies);
    }

    /// <summary>Текст для человека и модели.</summary>
    public static string Format(StackAnswer answer)
    {
        var builder = new StringBuilder().AppendLine($"Шаги ({answer.StepsSource}): {string.Join(" → ", answer.Steps.Select(step => $"«{step}»"))}");

        if (answer.Plans.Count == 0) return builder.Append("Стек не собран: нет шагов.").ToString();

        for (int p = 0; p < answer.Plans.Count; p++)
        {
            StackPlan plan = answer.Plans[p];
            builder.AppendLine().AppendLine($"Стек {p + 1} (оценка {plan.Score:F2}):");

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                StackStep step = plan.Steps[i];

                if (step.Hit is null)
                {
                    builder.AppendLine($"  {i + 1}. «{step.Need}» — ПРОБЕЛ: в библиотеке не найдено, писать своё (сначала dup_check).");
                    continue;
                }

                CodeUnit unit = step.Hit.Unit;
                builder.AppendLine($"  {i + 1}. «{step.Need}» → {unit.Signature}")
                    .AppendLine($"       {unit.Id}  {unit.File}:{unit.Line}{(unit.Script != null ? $"  AIScript: {unit.Script}" : "")}")
                    .AppendLine($"       {step.Link}; покрытие слов шага {step.Coverage:P0}");
            }

            if (p == 0)
            {
                if (answer.Dependencies.Length > 0) builder.AppendLine($"  Зависимости: {answer.Dependencies}");
                if (answer.Check is { } check)
                {
                    string verdict = check.Passed switch { true => "пройдена", false => "НЕ пройдена", null => "не выполнена" };
                    builder.AppendLine($"  Проверка {check.Kind}: {verdict}");
                    foreach (string line in check.Details.Split('\n')) builder.AppendLine("    " + line);
                }
            }
        }

        return builder.ToString().TrimEnd();
    }
}
