using AI.LLM.Agents.Planning;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using Serilog;

namespace AI.LLM.Agents.Orchestration;

/// <summary>
/// Приёмка шага по его критерию готовности (<see cref="PlanStep.DoneWhen"/>): модель отвечает,
/// удовлетворяет ли результат критерию.
/// </summary>
/// <remarks>
/// Планировщик требует <c>done_when</c> для каждого шага и объясняет модели, каким он должен
/// быть, — но проверить естественно-языковой критерий может только другая модель. Без такой
/// приёмки «выполнено» означает лишь то, что исполнитель отработал, и план шёл дальше с планом
/// работы вместо самой работы.
/// <para>
/// При сбое обращения к модели шаг ПРИНИМАЕТСЯ. Неработающая приёмка не должна становиться
/// источником ложных провалов: каждый из них стоит повтора шага, а затем перепланирования всей
/// задачи.
/// </para>
/// </remarks>
public sealed class LlmStepValidator : IAsyncStepValidator
{
    private const string Yes = "да";

    private readonly ILLMClient _llm;
    private readonly GenerateSettings _settings;
    private readonly int _maxAnswerChars;

    /// <param name="llm">Клиент модели, выполняющей приёмку.</param>
    /// <param name="settings">
    /// Настройки генерации. <c>null</c> — температура 0 и короткий ответ: приёмке нужно решение,
    /// а не рассуждение.
    /// </param>
    /// <param name="maxAnswerChars">
    /// До скольких символов урезать проверяемый ответ. Критерий готовности почти всегда виден по
    /// началу результата, а полный текст шага может быть очень длинным.
    /// </param>
    public LlmStepValidator(ILLMClient llm, GenerateSettings settings = null, int maxAnswerChars = 4000)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _settings = settings;
        _maxAnswerChars = Math.Max(200, maxAnswerChars);
    }

    /// <inheritdoc/>
    public async Task<bool> IsSuccessAsync(
        PlanStep step, AgentResult result, CancellationToken cancellationToken = default)
    {
        if (result is null)
            return false;

        var answer = result.Answer?.Trim();
        if (string.IsNullOrEmpty(answer))
            return false;

        // Критерий шага, а при его отсутствии — само задание шага.
        var criterion = !string.IsNullOrWhiteSpace(step?.DoneWhen)
            ? step.DoneWhen.Trim()
            : step?.Description?.Trim();

        // Принимать нечем — принимаем: выдумывать критерий приёмка не вправе.
        if (string.IsNullOrWhiteSpace(criterion))
            return true;

        if (answer.Length > _maxAnswerChars)
            answer = answer[.._maxAnswerChars] + "…";

        try
        {
            var verdict = await _llm
                .SendAsync(BuildMessages(criterion, answer), Settings(), cancellationToken)
                .ConfigureAwait(false);

            return IsAccepted(verdict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "LlmStepValidator: приёмка шага {StepId} не отработала, шаг принят", step?.Id);
            return true;
        }
    }

    private GenerateSettings Settings() =>
        _settings?.Clone() ?? new GenerateSettings(temperature: 0.0, maxTokens: 16);

    private static List<LLMMessage> BuildMessages(string criterion, string answer) =>
    [
        LLMMessage.CreateMessage(
            Roles.System,
            "Ты — приёмка результата. Тебе дают КРИТЕРИЙ ГОТОВНОСТИ и РЕЗУЛЬТАТ работы.\n"
            + "Ответь одним словом: ДА — если результат удовлетворяет критерию, НЕТ — если нет.\n"
            + "План работы, обещание сделать позже, отчёт об отказе и пересказ задания критерию "
            + "НЕ удовлетворяют. Ничего, кроме одного слова, не пиши."),

        LLMMessage.CreateMessage(
            Roles.User,
            $"КРИТЕРИЙ ГОТОВНОСТИ:\n{criterion}\n\nРЕЗУЛЬТАТ:\n{answer}"),
    ];

    /// <summary>
    /// Разбирает вердикт. Пустой либо неразборчивый ответ — принято: см. про ложные провалы
    /// в описании класса.
    /// </summary>
    private static bool IsAccepted(string verdict)
    {
        if (string.IsNullOrWhiteSpace(verdict))
            return true;

        var text = verdict.Trim().ToLowerInvariant();

        // Проверяем «нет» первым: «да» является подстрокой многих слов, а вот явный отказ
        // модель пишет коротко и в начале.
        if (text.StartsWith("нет") || text.StartsWith("no"))
            return false;

        return text.StartsWith(Yes) || text.StartsWith("yes") || !text.Contains("нет");
    }
}
