using System.Diagnostics;
using AI.LLM.Agents.Guards;
using AI.LLM.Agents.Memory;
using AI.LLM.Agents.Multimodal;
using AI.LLM.Agents.Tools;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;
using Serilog;

namespace AI.LLM.Agents;

/// <summary>
/// Автономный AI-агент с мультимодальным циклом Observe-Reason-Act.
/// Все LLM-вызовы проходят через <see cref="ILLMClient"/> — биллинг сохраняется.
/// Поддерживает native function calling и prompt-based fallback для моделей без FC.
/// При подключённом <see cref="IObservationProvider"/> после выполнения инструментов
/// агент запрашивает наблюдение (скриншот, камера) и передаёт изображения в LLM.
/// <see cref="AgentResult.Usage"/> содержит полную статистику: LLM-токены + вызовы инструментов.
/// </summary>
public sealed partial class Agent
{
    private readonly ILLMClient _llm;
    private readonly ToolRegistry _tools;
    private readonly IAgentMemory _memory;
    private readonly IAgentGuard _guard;
    private readonly IObservationProvider _observer;
    private readonly AgentConfig _config;

    /// <summary>Вызывается после каждого шага (итерации) агента.</summary>
    public event EventHandler<AgentStep> OnStepCompleted;

    /// <summary>Вызывается после выполнения каждого инструмента.</summary>
    public event EventHandler<ToolExecutionResult> OnToolExecuted;

    /// <summary>Вызывается после полного завершения работы агента.</summary>
    public event EventHandler<AgentResult> OnCompleted;

    internal Agent(ILLMClient llm, ToolRegistry tools, IAgentMemory memory,
        IAgentGuard guard, IObservationProvider observer, AgentConfig config)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools;
        _memory = memory;
        _guard = guard;
        _observer = observer;
        _config = config ?? new AgentConfig();
    }

    /// <summary>Запускает агент с текстовым запросом.</summary>
    public Task<AgentResult> RunAsync(string userQuery, CancellationToken cancellationToken = default)
        => RunAsync(new AgentQuery(userQuery), cancellationToken);

    /// <summary>
    /// Запускает агент с мультимодальным запросом (текст + изображения).
    /// Поддерживает цикл Observe-Reason-Act для Computer Use и робототехники.
    /// </summary>
    public async Task<AgentResult> RunAsync(AgentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.Text))
            throw new ArgumentException("Запрос не может быть пустым.", nameof(query));

        var sw = Stopwatch.StartNew();
        var steps = new List<AgentStep>();
        var usage = new AgentUsage();

        var messages = await BuildInitialMessagesAsync(query).ConfigureAwait(false);

        bool promptFallback = _config.UsePromptFallback && _tools is { Count: > 0 };

        if (promptFallback)
            AugmentSystemPromptWithTools(messages);

        var settings = new GenerateSettings(temperature: _config.Temperature, maxTokens: _config.MaxTokens);

        if (!promptFallback && _tools is { Count: > 0 })
        {
            settings.Tools = _tools.GetDefinitions();
            settings.ToolChoice = ToolChoice.Auto();
        }

        for (int i = 0; i < _config.MaxIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await _llm.SendFullAsync(messages, settings, cancellationToken).ConfigureAwait(false);
            usage.AddLlmUsage(response?.Usage);

            if (response?.Choices is not { Count: > 0 })
            {
                Log.Warning("Agent: LLM вернул пустой ответ на итерации {Iteration}", i + 1);
                break;
            }

            var choice = response.Choices[0];
            var assistantMsg = choice.Message;

            if (assistantMsg == null)
            {
                Log.Warning("Agent: LLM вернул null Message на итерации {Iteration}", i + 1);
                break;
            }

            var toolCalls = assistantMsg.ToolCalls;
            bool hasToolCalls = toolCalls is { Count: > 0 };

            if (!hasToolCalls && promptFallback)
            {
                toolCalls = TryParseToolCallsFromText(assistantMsg.Content?.ToString());
                hasToolCalls = toolCalls is { Count: > 0 };
                if (hasToolCalls)
                    assistantMsg.ToolCalls = toolCalls;
            }

            if (!hasToolCalls)
                return await FinishAsync(query.Text, assistantMsg, choice, steps, i + 1, usage, sw, cancellationToken)
                    .ConfigureAwait(false);

            messages.Add(assistantMsg);

            var results = await ExecuteToolsAsync(toolCalls, cancellationToken).ConfigureAwait(false);
            usage.AddToolResults(results);
            messages.AddRange(ToolRegistry.ToToolMessages(results));

            AgentObservation observation = null;
            if (_observer != null && _config.ObserveAfterToolExecution)
                observation = await ObserveAndAppendAsync(messages, cancellationToken).ConfigureAwait(false);

            var step = new AgentStep
            {
                StepNumber = i + 1,
                AssistantMessage = assistantMsg.Content?.ToString(),
                Reasoning = assistantMsg.Reasoning,
                ToolCalls = toolCalls,
                ToolResults = results,
                Observation = observation,
                FinishReason = choice.FinishReason
            };
            steps.Add(step);
            OnStepCompleted?.Invoke(this, step);
        }

        sw.Stop();
        var maxIterResult = new AgentResult(
            $"Достигнут лимит итераций ({_config.MaxIterations}).",
            steps, sw.Elapsed, usage);
        OnCompleted?.Invoke(this, maxIterResult);
        return maxIterResult;
    }

    /// <summary>Формирует финальный результат, проверяет guard, сохраняет в память.</summary>
    private async Task<AgentResult> FinishAsync(
        string userQuery, LLMMessage assistantMsg, Choice choice,
        List<AgentStep> steps, int stepNumber, AgentUsage usage,
        Stopwatch sw, CancellationToken ct)
    {
        var answer = assistantMsg.Content?.ToString() ?? string.Empty;

        var step = new AgentStep
        {
            StepNumber = stepNumber,
            AssistantMessage = answer,
            Reasoning = assistantMsg.Reasoning,
            FinishReason = choice.FinishReason
        };
        steps.Add(step);
        OnStepCompleted?.Invoke(this, step);

        if (_guard != null)
        {
            var guardResult = await _guard.CheckAsync(userQuery, answer, ct).ConfigureAwait(false);
            if (!guardResult.Passed)
                Log.Warning("Agent Guard отклонил ответ: {Reason} (score: {Score:F2})", guardResult.Reason, guardResult.Score);
        }

        if (_memory != null)
            await _memory.SaveInteractionAsync(userQuery, answer, null).ConfigureAwait(false);

        sw.Stop();
        var result = new AgentResult(answer, steps, sw.Elapsed, usage);
        OnCompleted?.Invoke(this, result);
        return result;
    }

    /// <summary>Параллельно выполняет все tool_calls, пробрасывает CancellationToken.</summary>
    private async Task<List<ToolExecutionResult>> ExecuteToolsAsync(
        List<ToolCall> toolCalls, CancellationToken ct)
    {
        if (_tools == null || toolCalls is not { Count: > 0 })
            return [];

        var results = await _tools.ExecuteParallelAsync(toolCalls, ct).ConfigureAwait(false);

        foreach (var r in results)
            OnToolExecuted?.Invoke(this, r);

        return results;
    }

    #region Мультимодальность

    /// <summary>Формирует начальные сообщения: system + user (с изображениями если есть).</summary>
    private async Task<List<LLMMessage>> BuildInitialMessagesAsync(AgentQuery query)
    {
        if (_memory != null)
            return await _memory.BuildContextAsync(query.Text, _config.SystemPrompt).ConfigureAwait(false);

        return
        [
            LLMMessage.CreateMessage(Roles.System, _config.SystemPrompt),
            BuildUserMessage(query.Text, query.Images)
        ];
    }

    /// <summary>
    /// Создаёт user-сообщение: если есть изображения — через MessageContent (multimodal),
    /// иначе обычный текст.
    /// </summary>
    private static LLMMessage BuildUserMessage(string text, IReadOnlyList<AgentImage> images)
    {
        if (images is not { Count: > 0 })
            return LLMMessage.CreateMessage(Roles.User, text);

        var mc = new MessageContent(text);
        foreach (var img in images)
            mc.AddImage(img.Data);
        return new LLMMessage(LLMMessage.UserRole, mc);
    }

    /// <summary>
    /// Запрашивает наблюдение у IObservationProvider и добавляет изображения в контекст.
    /// </summary>
    private async Task<AgentObservation> ObserveAndAppendAsync(
        List<LLMMessage> messages, CancellationToken ct)
    {
        try
        {
            var observation = await _observer.ObserveAsync(ct).ConfigureAwait(false);
            if (observation?.Images is not { Count: > 0 })
                return observation;

            var imagesToInclude = observation.Images
                .Take(_config.MaxObservationImages)
                .ToList();

            var description = string.IsNullOrEmpty(observation.Description)
                ? "[Observation]"
                : $"[Observation: {observation.Description}]";

            var mc = new MessageContent(description);
            foreach (var img in imagesToInclude)
                mc.AddImage(img.Data);

            messages.Add(new LLMMessage(LLMMessage.UserRole, mc));

            Log.Debug("Agent: наблюдение добавлено ({Count} изображений): {Description}",
                imagesToInclude.Count, observation.Description);

            return observation;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log.Error(ex, "Agent: ошибка при получении наблюдения");
            return null;
        }
    }

    #endregion

    /// <summary>Внедряет описания инструментов в system-промпт (для prompt fallback).</summary>
    private void AugmentSystemPromptWithTools(List<LLMMessage> messages)
    {
        var toolBlock = BuildToolPromptBlock(_tools);
        if (messages.Count > 0 && messages[0].Role == LLMMessage.SystemRole)
            messages[0] = LLMMessage.CreateMessage(Roles.System, messages[0].Content?.ToString() + toolBlock);
    }
}
