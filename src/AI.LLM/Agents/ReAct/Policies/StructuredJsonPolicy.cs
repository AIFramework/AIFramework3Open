using System.Text;
using System.Text.Json;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Agents.ReAct.Tools;

namespace AI.LLM.Agents.ReAct.Policies;

/// <summary>
/// Решение шага через структурированный текст: модель отвечает JSON-объектом
/// <c>{"thought","action","action_input"}</c> либо <c>{"thought","final"}</c>.
/// <para>
/// Работает с любым поставщиком, включая тех, кто не умеет нативные вызовы инструментов, —
/// нужен только текстовый ответ. Разбор терпим к обрамлению и к синонимам полей: модели
/// устойчиво называют аргумент то <c>action_input</c>, то <c>input</c>, то <c>query</c>.
/// </para>
/// </summary>
public sealed class StructuredJsonPolicy : IReActPolicy
{
    private const string DefaultContract =
        "Ответь СТРОГО одним JSON-объектом, без текста вне него:\n"
        + "{\"thought\":\"кратко, зачем это действие\",\"action\":\"имя инструмента\",\"action_input\":\"аргумент\"}\n"
        + "либо, когда данных достаточно:\n"
        + "{\"thought\":\"кратко\",\"final\":\"краткий ответ\"}";

    /// <summary>
    /// «Отдавать историю как есть» для <c>historyMessages</c> и <c>historyMessageChars</c>.
    /// </summary>
    /// <remarks>
    /// Умолчания (шесть сообщений по 300 знаков) рассчитаны на вызывающего, который передаёт
    /// сырую историю. Вызывающий, который её УЖЕ отобрал по своим правилам, обязан сказать об
    /// этом явно: иначе окно и обрезка применяются второй раз и молча отменяют его отбор —
    /// врезки хода вытесняются хвостом диалога, а поднятые потолки реплик срезаются до 300.
    /// </remarks>
    public const int AsGiven = int.MaxValue;

    private static readonly string[] ActionInputNames = ["action_input", "input", "arg", "arguments", "query", "text"];
    private static readonly string[] FinalNames = ["final", "final_answer", "answer", "output"];
    private static readonly string[] FinalActions = ["final", "finish", "done", "answer", "stop"];

    private readonly ReActCompletionDelegate _complete;
    private readonly string _contract;
    private readonly int _historyMessages;
    private readonly int _historyMessageChars;

    /// <summary>Создаёт реализацию поверх произвольного обращения к модели.</summary>
    /// <param name="complete">Обращение к модели.</param>
    /// <param name="contract">Описание формата ответа; при <c>null</c> берётся стандартное.</param>
    /// <param name="historyMessages">
    /// Сколько последних сообщений истории показывать модели. <see cref="AsGiven"/> — все:
    /// историю отобрал вызывающий, и второе окно поверх его отбора выбрасывает то, что он
    /// поставил в начало списка.
    /// </param>
    /// <param name="historyMessageChars">
    /// Предел длины одного сообщения истории. <see cref="AsGiven"/> — не резать: у вызывающего
    /// свои потолки на роль, и общий предел здесь их отменяет.
    /// </param>
    public StructuredJsonPolicy(
        ReActCompletionDelegate complete,
        string contract = null,
        int historyMessages = 6,
        int historyMessageChars = 300)
    {
        _complete = complete ?? throw new ArgumentNullException(nameof(complete));
        _contract = string.IsNullOrWhiteSpace(contract) ? DefaultContract : contract;
        _historyMessages = Math.Max(0, historyMessages);
        _historyMessageChars = Math.Max(1, historyMessageChars);
    }

    /// <summary>Создаёт реализацию поверх клиента библиотеки.</summary>
    /// <param name="llm">Клиент модели.</param>
    /// <param name="settings">Настройки генерации; при <c>null</c> берутся умеренные значения.</param>
    /// <param name="requestJsonResponseFormat">Просить ли у поставщика режим строгого JSON.</param>
    public StructuredJsonPolicy(ILLMClient llm, GenerateSettings settings = null, bool requestJsonResponseFormat = true)
        : this(BuildCompletion(llm, settings, requestJsonResponseFormat))
    {
    }

    /// <inheritdoc />
    public async Task<ReActDecision> DecideAsync(
        ReActPolicyContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        string user = BuildUserPrompt(context);

        string raw;
        try
        {
            raw = await _complete(context.SystemPrompt, user, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "ReAct: обращение к модели на шаге {Step} не удалось", context.StepNumber);
            return ReActDecision.Malformed(null);
        }

        return Parse(raw);
    }

    /// <summary>
    /// Разбирает ответ модели в решение шага. Открыт намеренно: тот же разбор нужен тем,
    /// кто пишет собственную реализацию принятия решений поверх своего клиента.
    /// </summary>
    /// <param name="raw">Сырой ответ модели.</param>
    /// <returns>
    /// Решение шага. Неразобранный ответ даёт <see cref="ReActDecision.Malformed"/>,
    /// а НЕ завершение цикла.
    /// </returns>
    public static ReActDecision Parse(string raw)
    {
        using JsonDocument document = ReActJsonParser.TryParseObject(raw);
        if (document == null)
            return ReActDecision.Malformed(raw);

        JsonElement root = document.RootElement;
        string thought = ReActJsonParser.FirstString(root, "thought", "reasoning", "reason");

        bool hasFinal = ReActJsonParser.TryGetFinal(root, FinalNames, out string final);
        string action = ReActJsonParser.FirstString(root, "action", "tool", "tool_name", "name");

        bool actionIsFinal = action != null
                             && Array.Exists(
                                 FinalActions,
                                 name => string.Equals(name, action.Trim(), StringComparison.OrdinalIgnoreCase));

        if (hasFinal || actionIsFinal)
            return ReActDecision.Final(final, thought, rawResponse: raw);

        if (string.IsNullOrWhiteSpace(action))
            return ReActDecision.Malformed(raw);

        string input = ReActJsonParser.FirstString(root, ActionInputNames) ?? string.Empty;
        return ReActDecision.Act(new ReActAction(action, input), thought, rawResponse: raw);
    }

    private string BuildUserPrompt(ReActPolicyContext context)
    {
        var sb = new StringBuilder();

        IReadOnlyList<LLMMessage> history = context.Query?.History ?? [];
        if (_historyMessages > 0 && history.Count > 0)
        {
            sb.Append("Недавняя история:\n");
            int from = Math.Max(0, history.Count - _historyMessages);
            for (int i = from; i < history.Count; i++)
            {
                LLMMessage message = history[i];
                string text = message.Content?.ToString() ?? string.Empty;
                if (text.Length > _historyMessageChars)
                    text = text[.._historyMessageChars] + "…";

                sb.Append(message.Role).Append(": ").Append(text).Append('\n');
            }

            sb.Append('\n');
        }

        sb.Append("Запрос: ").Append(context.Query?.Text).Append("\n\n");

        sb.Append(string.IsNullOrWhiteSpace(context.RenderedTrace)
            ? "Наблюдений пока нет.\n"
            : "Наблюдения:\n" + context.RenderedTrace);

        if (!string.IsNullOrWhiteSpace(context.CorrectiveNote))
            sb.Append('\n').Append(context.CorrectiveNote).Append('\n');

        if (context.MaxSteps > 0)
            sb.Append("\nШаг ").Append(context.StepNumber).Append(" из ").Append(context.MaxSteps).Append(".\n");

        sb.Append('\n').Append(_contract);
        return sb.ToString();
    }

    private static ReActCompletionDelegate BuildCompletion(
        ILLMClient llm, GenerateSettings settings, bool requestJsonResponseFormat)
    {
        ArgumentNullException.ThrowIfNull(llm);

        return async (system, user, ct) =>
        {
            // Копия: настройки общие на все обращения, а формат ответа доопределяется здесь.
            GenerateSettings effective = settings?.Clone() ?? new GenerateSettings(temperature: 0.0, maxTokens: 800);
            if (requestJsonResponseFormat)
                effective.ResponseFormat ??= ResponseFormat.CreateJsonObject();

            var messages = new List<LLMMessage>
            {
                LLMMessage.CreateMessage(Roles.System, system),
                LLMMessage.CreateMessage(Roles.User, user),
            };

            return await llm.SendAsync(messages, effective, ct).ConfigureAwait(false);
        };
    }
}
