using System.Text.Json;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Помощники итога вызова модели: одно правило на все реализации <see cref="IModelPort"/>.
/// </summary>
public static class LlmAnswers
{
    /// <summary>
    /// Правило ответа по схеме (<see cref="ModelAsk.Schema"/>): итог <see cref="LlmStatus.Ok"/> обязан нести
    /// разбираемый JSON. Ответ не JSON становится <see cref="LlmStatus.Failed"/> с кодом
    /// <see cref="LlmFailure.InvalidOutput"/>; текст ответа переезжает в <see cref="LlmAnswer.FailureText"/>, расход и
    /// модель сохраняются. Остальные итоги (пустой, оборванный, сбой) возвращаются как есть: пустой ответ остается
    /// <see cref="LlmStatus.Empty"/>, а не сбоем.
    /// </summary>
    /// <param name="answer">Итог вызова с <see cref="ModelAsk.Schema"/>.</param>
    /// <returns>Тот же итог или итог со сбоем <see cref="LlmFailure.InvalidOutput"/>.</returns>
    public static LlmAnswer RequireJson(LlmAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.Status != LlmStatus.Ok || answer.Text == null)
            return answer;

        try
        {
            using JsonDocument _ = JsonDocument.Parse(answer.Text);
            return answer;
        }
        catch (JsonException)
        {
            return answer with
            {
                Status = LlmStatus.Failed,
                Failure = LlmFailure.InvalidOutput,
                FailureText = "ответ по схеме не разбирается как JSON: " + Shorten(answer.Text),
                Text = null,
            };
        }
    }

    private static string Shorten(string text) => text.Length <= 200 ? text : text[..200] + "…";
}
