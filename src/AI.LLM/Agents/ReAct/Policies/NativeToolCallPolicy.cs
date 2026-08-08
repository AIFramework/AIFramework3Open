using System.Text.Json;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
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

    private readonly ILLMClient _llm;
    private readonly GenerateSettings _settings;

    /// <summary>Создаёт реализацию.</summary>
    /// <param name="llm">Клиент модели; должен поддерживать вызовы инструментов.</param>
    /// <param name="settings">Настройки генерации; при <c>null</c> берутся умеренные значения.</param>
    public NativeToolCallPolicy(ILLMClient llm, GenerateSettings settings = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<ReActDecision> DecideAsync(
        ReActPolicyContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<LLMMessage> messages = BuildMessages(context);

        // Копия, а не общий экземпляр: набор инструментов считается на каждый прогон, а движок
        // рассчитан на несколько одновременных. Правка общих настроек означала бы, что соседний
        // прогон уходит к модели с чужим списком инструментов.
        GenerateSettings settings = _settings?.Clone() ?? new GenerateSettings(temperature: 0.1, maxTokens: 1200);
        settings.Tools = BuildDefinitions(context.Tools);
        settings.ToolChoice = ToolChoice.Auto();

        ChatCompletionsResponse response;
        try
        {
            response = await _llm.SendFullAsync(messages, settings, cancellationToken).ConfigureAwait(false);
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
    /// </summary>
    private static List<LLMMessage> BuildMessages(ReActPolicyContext context)
    {
        var messages = new List<LLMMessage> { LLMMessage.CreateMessage(Roles.System, context.SystemPrompt) };

        foreach (LLMMessage message in context.Query?.History ?? [])
            messages.Add(message);

        messages.Add(LLMMessage.CreateMessage(Roles.User, context.Query?.Text ?? string.Empty));

        foreach (ReActStep step in context.Trace?.Steps ?? [])
        {
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
                        Arguments = observation.Action.Arguments,
                    },
                });
            }

            if (toolCalls.Count == 0)
                continue;

            var assistant = new LLMMessage(LLMMessage.AssistantRole, step.Thought ?? string.Empty)
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

                messages.Add(LLMMessage.CreateToolResult(observation.Action.Id, observation.Text ?? string.Empty));
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
