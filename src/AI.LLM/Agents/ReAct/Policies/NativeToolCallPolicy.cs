using System.Text.Json;
using AI.LLM.Agents.Multimodal;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;

namespace AI.LLM.Agents.ReAct.Policies;

/// <summary>
/// Решение шага через нативные вызовы инструментов: список инструментов уходит поставщику
/// в поле <c>tools</c>, а модель отвечает структурой <c>tool_calls</c>.
/// <para>
/// Надёжнее текстового протокола там, где модель это умеет: имена и аргументы приходят
/// разобранными, а не выуживаются из текста.
/// </para>
/// </summary>
/// <remarks>
/// Реализация не хранит состояние: список сообщений собирается заново из следа на каждом шаге.
/// Это важно для протокола — на каждый вызов инструмента обязан прийти ответ с тем же
/// идентификатором, и восстановление из следа гарантирует, что ни один не потеряется.
/// </remarks>
public sealed class NativeToolCallPolicy : IReActPolicy
{
    /// <summary>Имя единственного параметра у инструментов без собственной схемы.</summary>
    internal const string PlainArgumentName = "input";

    /// <summary>Длиннее этого сообщение модели, аргументы и результат старого шага сверх предела следа заменяются пометкой.</summary>
    private const int ElidedChars = 300;

    /// <summary>Аргументы старого вызова сверх предела следа: остаются корректным JSON, иначе часть поставщиков отвергает запрос.</summary>
    private const string ElidedArguments = "{\"опущено\":\"аргументы старого шага не пересылаются ради места\"}";

    private readonly ILLMClient _llm;
    private readonly GenerateSettings _settings;
    private readonly bool _cacheSystemPrompt;

    /// <summary>Создаёт реализацию.</summary>
    /// <param name="llm">Клиент модели; должен поддерживать вызовы инструментов.</param>
    /// <param name="settings">Настройки генерации; при <c>null</c> берутся умеренные значения.</param>
    /// <param name="cacheSystemPrompt">Помечать системный промпт для кеша поставщика (<c>cache_control</c>).</param>
    public NativeToolCallPolicy(ILLMClient llm, GenerateSettings settings = null, bool cacheSystemPrompt = false)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _settings = settings;
        _cacheSystemPrompt = cacheSystemPrompt;
    }

    /// <inheritdoc />
    /// <remarks>Описания уходят полем <c>tools</c> запроса.</remarks>
    public bool DescribesTools => true;

    /// <inheritdoc />
    public async Task<ReActDecision> DecideAsync(
        ReActPolicyContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<LLMMessage> messages = BuildMessages(context, _cacheSystemPrompt);

        // Копия, а не общий экземпляр: набор инструментов считается на каждый прогон, а движок
        // рассчитан на несколько одновременных. Правка общих настроек означала бы, что соседний
        // прогон уходит к модели с чужим списком инструментов.
        GenerateSettings settings = _settings?.Clone() ?? new GenerateSettings(temperature: 0.1, maxTokens: 1200);
        settings.Tools = BuildDefinitions(context.Tools);
        settings.ToolChoice = ToolChoice.Auto();

        ChatCompletionsResponse response;
        try
        {
            Action<LLMStreamChunk> onChunk = context.OnText == null
                ? null
                : chunk => context.OnText(new ReActTextChunk(chunk.Content, chunk.Reasoning));
            response = await _llm.SendFullAsync(messages, settings, onChunk, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AI.LLM.Clients.Base.LlmRequestException ex) when (ex.IsEmptyAnswer)
        {
            // Модель закончила ответ молча (бывает после результата инструмента): это не отказ сервера, а пустое
            // решение — цикл напомнит, что нужен ответ или вызов инструмента, и спросит снова.
            Serilog.Log.Warning("ReAct: пустой ответ модели на шаге {Step}", context.StepNumber);
            return ReActDecision.Malformed(null);
        }
        catch (AI.LLM.Clients.Base.LlmRequestException)
        {
            // Отказ сервера (ключ, баланс, окно, недоступность после повторов) — не «неверный формат»:
            // подсказка формата модели не поможет, а человек должен увидеть причину как есть.
            throw;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "ReAct: обращение к модели на шаге {Step} не удалось", context.StepNumber);
            return ReActDecision.Malformed(null);
        }

        if (response?.Choices is not { Count: > 0 })
            return ReActDecision.Malformed(null);

        Choice choice = response.Choices[0];
        LLMMessage message = choice.Message;
        if (message == null)
            return ReActDecision.Malformed(null, response.Usage);

        string content = message.Content?.ToString();

        if (message.ToolCalls is not { Count: > 0 })
            return ReActDecision.Final(content, message.Reasoning, response.Usage, content);

        var actions = new List<ReActAction>(message.ToolCalls.Count);
        foreach (ToolCall call in message.ToolCalls)
        {
            string name = call?.Function?.Name;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string arguments = Unwrap(name, call.Function.Arguments, context.Tools);
            actions.Add(new ReActAction(name, arguments, call.Id));
        }

        return actions.Count == 0
            ? ReActDecision.Malformed(content, response.Usage)
            : ReActDecision.Act(actions, message.Reasoning ?? content, response.Usage, content);
    }

    /// <summary>
    /// Восстанавливает переписку из следа: системная инструкция, история, запрос, затем на
    /// каждый шаг — сообщение ассистента с вызовами и по одному ответу на каждый вызов.
    /// <para>
    /// След пересылается на каждом шаге целиком, поэтому соблюдает <see cref="ReActPolicyContext.MaxTraceChars"/>:
    /// свежие шаги идут полностью, у более старых результат, длинные аргументы и сообщение модели
    /// заменяются пометкой. Сами вызовы и ответы на них остаются — без пары с тем же
    /// идентификатором поставщик отверг бы запрос.
    /// </para>
    /// <para>
    /// Картинки запроса идут вместе с его текстом. Картинки инструментов (снимок формы, кадр камеры)
    /// ответ роли <c>tool</c> у большинства поставщиков не несёт, поэтому они идут следом сообщением
    /// пользователя — и только у последнего шага, где они были: прежние снимки модель уже видела, а
    /// пересылка на каждом шаге стоила бы как новые.
    /// </para>
    /// </summary>
    private static List<LLMMessage> BuildMessages(ReActPolicyContext context, bool cacheSystemPrompt)
    {
        // Метка кеша — на системном промпте: вместе с инструментами перед ним это постоянный
        // префикс каждого шага, и поставщик дальше считает его по цене чтения кеша.
        LLMMessage system = cacheSystemPrompt
            ? new LLMMessage(LLMMessage.SystemRole, new MessageContent { new TextContentItem(context.SystemPrompt) { CacheControl = CacheControl.Ephemeral } })
            : LLMMessage.CreateMessage(Roles.System, context.SystemPrompt);
        var messages = new List<LLMMessage> { system };

        foreach (LLMMessage message in context.Query?.History ?? [])
            messages.Add(message);

        messages.Add(AgentImage.UserMessage(context.Query?.Text ?? string.Empty, context.Query?.Images));

        IReadOnlyList<ReActStep> steps = context.Trace?.Steps ?? [];
        int firstFull = FirstFullStep(steps, context.MaxTraceChars);
        int imageStep = LastStepWithImages(steps);
        for (int index = 0; index < steps.Count; index++)
        {
            ReActStep step = steps[index];
            bool full = index >= firstFull;
            if (step.Observations.Count == 0)
                continue;

            var toolCalls = new List<ToolCall>(step.Observations.Count);
            foreach (ReActObservation observation in step.Observations)
            {
                if (observation.Action == null)
                    continue;

                toolCalls.Add(new ToolCall
                {
                    Id = observation.Action.Id,
                    Function = new FunctionCall
                    {
                        Name = observation.Action.ToolName,
                        Arguments = full || (observation.Action.Arguments?.Length ?? 0) <= ElidedChars
                            ? observation.Action.Arguments
                            : ElidedArguments,
                    },
                });
            }

            if (toolCalls.Count == 0)
                continue;

            // Модели возвращается её сообщение, а не рассуждения: они уже сделали своё дело и на
            // каждом следующем шаге оплачивались бы как вход заново.
            string thought = step.Reply ?? string.Empty;
            if (!full && thought.Length > ElidedChars)
                thought = thought[..ElidedChars] + "…";

            var assistant = new LLMMessage(LLMMessage.AssistantRole, thought)
            {
                ToolCalls = toolCalls,
            };
            messages.Add(assistant);

            // На каждый вызов — ровно один ответ с тем же идентификатором: без этого
            // поставщик отвергает следующий запрос целиком.
            foreach (ReActObservation observation in step.Observations)
            {
                if (observation.Action == null)
                    continue;

                string text = observation.Text ?? string.Empty;
                if (!full && text.Length > ElidedChars)
                    text = $"[Результат старого шага опущен ради места: {text.Length} знаков.]";

                messages.Add(LLMMessage.CreateToolResult(observation.Action.Id, text));
            }

            if (index == imageStep)
            {
                List<ReActObservation> pictured = step.Observations.Where(o => o.Images is { Count: > 0 }).ToList();
                string names = string.Join(", ", pictured.Select(o => o.Action?.ToolName).Where(n => n != null).Distinct());
                messages.Add(AgentImage.UserMessage($"[Изображения из ответа инструмента {names}]", pictured.SelectMany(o => o.Images).ToList()));
            }

            if (!string.IsNullOrWhiteSpace(step.Note))
                messages.Add(LLMMessage.CreateMessage(Roles.User, step.Note));
        }

        if (!string.IsNullOrWhiteSpace(context.CorrectiveNote))
            messages.Add(LLMMessage.CreateMessage(Roles.User, context.CorrectiveNote));

        return messages;
    }

    private static List<ToolDefinition> BuildDefinitions(IReadOnlyList<IReActTool> tools)
    {
        var definitions = new List<ToolDefinition>(tools?.Count ?? 0);
        foreach (IReActTool tool in tools ?? [])
        {
            string schema = string.IsNullOrWhiteSpace(tool.ParametersJsonSchema)
                ? BuildPlainSchema(tool.Description)
                : tool.ParametersJsonSchema;

            definitions.Add(ToolDefinition.Create(tool.Name, tool.Description, schema));
        }

        return definitions;
    }

    /// <summary>Последний шаг, где инструмент вернул картинки; −1 — таких нет.</summary>
    private static int LastStepWithImages(IReadOnlyList<ReActStep> steps)
    {
        for (int i = steps.Count - 1; i >= 0; i--)
            if (steps[i].Observations.Any(o => o.Action != null && o.Images is { Count: > 0 }))
                return i;
        return -1;
    }

    /// <summary>
    /// С какого шага след идёт полностью: шаги набираются от последнего к первому, пока хватает
    /// <paramref name="maxChars"/>. Последний шаг идёт всегда — без своего свежего наблюдения
    /// модель повторила бы действие. 0 и меньше — все шаги полностью.
    /// </summary>
    private static int FirstFullStep(IReadOnlyList<ReActStep> steps, int maxChars)
    {
        if (maxChars <= 0)
            return 0;

        int used = 0;
        bool any = false;
        for (int i = steps.Count - 1; i >= 0; i--)
        {
            int size = StepChars(steps[i]);
            if (any && used + size > maxChars)
                return i + 1;

            used += size;
            any |= size > 0;
        }

        return 0;
    }

    /// <summary>Сколько знаков шаг занимает в переписке: сообщение модели, аргументы вызовов и их результаты.</summary>
    private static int StepChars(ReActStep step)
    {
        int size = step.Reply?.Length ?? 0;
        foreach (ReActObservation observation in step.Observations)
            size += (observation.Action?.Arguments?.Length ?? 0) + (observation.Text?.Length ?? 0);

        return size;
    }

    /// <summary>Схема для инструмента с одним свободным строковым аргументом.</summary>
    private static string BuildPlainSchema(string description)
    {
        string safe = JsonSerializer.Serialize(string.IsNullOrWhiteSpace(description)
            ? "Аргумент инструмента."
            : description);

        return "{\"type\":\"object\",\"properties\":{\"" + PlainArgumentName
               + "\":{\"type\":\"string\",\"description\":" + safe
               + "}},\"required\":[\"" + PlainArgumentName + "\"]}";
    }

    /// <summary>
    /// Разворачивает <c>{"input":"…"}</c> обратно в простую строку для инструментов без
    /// собственной схемы. Иначе один и тот же инструмент получал бы разный аргумент в
    /// зависимости от того, каким способом получено решение.
    /// </summary>
    private static string Unwrap(string toolName, string arguments, IReadOnlyList<IReActTool> tools)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return string.Empty;

        IReActTool tool = null;
        foreach (IReActTool candidate in tools ?? [])
        {
            if (string.Equals(candidate.Name, toolName, StringComparison.OrdinalIgnoreCase))
            {
                tool = candidate;
                break;
            }
        }

        if (tool == null || !string.IsNullOrWhiteSpace(tool.ParametersJsonSchema))
            return arguments;

        try
        {
            using JsonDocument document = JsonDocument.Parse(arguments);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(PlainArgumentName, out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
            // Не JSON — отдаём как есть.
        }

        return arguments;
    }
}
