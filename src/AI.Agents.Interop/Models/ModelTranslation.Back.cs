using System.Net;
using System.Text.Json;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;
using ContractToolCall = AI.Agents.Contracts.Tools.ToolCall;
using LlmToolCall = AI.LLM.Core.Models.Common.ToolCalling.ToolCall;

namespace AI.Agents.Interop.Models;

// Часть ModelTranslation: запрос старой формы в контракт и итог контракта в ответ старой формы.
public static partial class ModelTranslation
{
    // ------------------------------------------------------------------
    // Запрос: старая форма → контракт
    // ------------------------------------------------------------------

    /// <summary>Запрос старой формы в запрос контракта.</summary>
    /// <param name="messages">Сообщения.</param>
    /// <param name="settings">Настройки; <see langword="null"/>: без инструментов и формата.</param>
    /// <param name="role">Роль модели в порте.</param>
    /// <param name="lost">Куда сложить ключи непереведенных полей (температура, рассуждение и прочее); <see langword="null"/>: не собирать.</param>
    /// <returns>Запрос: инструменты из <c>Tools</c>, режим из <c>ToolChoice</c>, схема из <c>ResponseFormat</c>, предел токенов.</returns>
    /// <exception cref="ArgumentException">Имя инструмента не подходит под <see cref="ToolSpec.NamePattern"/>; роль не задана.</exception>
    /// <exception cref="NotSupportedException">Роль сообщения или часть содержимого, которых нет в контракте.</exception>
    public static ModelAsk ToAsk(
        IEnumerable<LLMMessage> messages, GenerateSettings? settings, ModelRole role, ICollection<string>? lost = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (role.Name == null)
            throw new ArgumentException("Роль модели не задана (default(ModelRole)).", nameof(role));

        IReadOnlyList<ToolSpec>? tools = settings?.Tools is { Count: > 0 } definitions
            ? [.. definitions.Select(ToSpec)]
            : null;

        ToolMode mode;
        string? toolName = null;
        switch (settings?.ToolChoice)
        {
            case null:
                mode = tools == null ? ToolMode.None : ToolMode.Auto;
                break;
            case { IsStringMode: false } named:
                mode = ToolMode.Named;
                toolName = named.FunctionName;
                break;
            case { Mode: "auto" }:
                mode = ToolMode.Auto;
                break;
            case { Mode: "none" }:
                mode = ToolMode.None;
                break;
            case { Mode: "required" }:
                mode = ToolMode.Required;
                break;
            case { } other:
                throw new NotSupportedException($"Выбор инструмента «{other.Mode}» в контракте не выражается.");
        }

        JsonElement? schema = null;
        switch (settings?.ResponseFormat)
        {
            case null:
                break;
            case { Type: "json_schema", JsonSchema: { } definition }:
                schema = definition.Schema.Clone();
                if (definition.Strict == false)
                    lost?.Add("JsonSchemaDefinition.Strict");
                break;
            default:
                lost?.Add("GenerateSettings.ResponseFormat");
                break;
        }

        if (settings != null && lost != null)
            CollectLostSettings(settings, lost);

        return new ModelAsk
        {
            Temperature = settings?.Temperature,
            Reasoning = ReasoningOf(settings),
            Role = role,
            Messages = [.. messages.Select(m => ToModelMessage(m, lost))],
            ToolMode = mode,
            Tools = tools,
            ToolName = toolName,
            Schema = schema,
            MaxTokens = settings?.MaxTokens,
        };
    }

    /// <summary>Сообщение старой формы в сообщение контракта.</summary>
    /// <param name="message">Сообщение старой формы.</param>
    /// <param name="lost">Куда сложить ключи непереведенных полей; <see langword="null"/>: не собирать.</param>
    /// <returns>
    /// Сообщение контракта. Происхождение каждой части — <see cref="OriginKind.Unknown"/>: старая форма его не знает,
    /// а доверенным такой текст контракт не считает. Метка кэша на любой текстовой части — <see cref="ModelMessage.Cache"/>.
    /// </returns>
    /// <exception cref="NotSupportedException">Роль, кроме system, user, assistant и tool; содержимое неизвестного вида.</exception>
    public static ModelMessage ToModelMessage(this LLMMessage message, ICollection<string>? lost = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        MessageRole role = message.Role?.ToLowerInvariant() switch
        {
            LLMMessage.SystemRole => MessageRole.System,
            LLMMessage.UserRole => MessageRole.User,
            LLMMessage.AssistantRole => MessageRole.Assistant,
            LLMMessage.ToolRole => MessageRole.Tool,
            _ => throw new NotSupportedException($"Роль сообщения «{message.Role}» в контракте не выражается."),
        };

        bool cache = false;
        IReadOnlyList<MessagePart>? parts = message.Content switch
        {
            null => null,
            string text => [new MessagePart.Text { Content = text, Origin = UnknownOrigin }],
            MessageContent items => [.. items.Select(item => ToPart(item, ref cache))],
            var other => throw new NotSupportedException($"Содержимое сообщения вида {other.GetType().Name} в контракте не выражается."),
        };

        if (lost != null)
        {
            if (message.Images is { Count: > 0 }) lost.Add("LLMMessage.Images");
            if (!string.IsNullOrEmpty(message.Refusal)) lost.Add("LLMMessage.Refusal");
            if (!string.IsNullOrEmpty(message.Reasoning)) lost.Add("LLMMessage.Reasoning");
            if (!string.IsNullOrEmpty(message.Name)) lost.Add("LLMMessage.Name");
        }

        return new ModelMessage
        {
            Role = role,
            Parts = parts,
            ToolCalls = message.ToolCalls is { Count: > 0 } calls ? [.. calls.Select(c => ToToolCall(c, lost))] : null,
            ToolCallId = message.ToolCallId,
            Cache = cache,
        };
    }

    /// <summary>Определение функции старой формы в описание инструмента контракта.</summary>
    /// <param name="definition">Определение.</param>
    /// <returns>
    /// Описание с <see cref="ToolAction.Unknown"/>: модели вид действия не нужен, а исполняет инструменты не порт.
    /// Без схемы параметров — пустая схема объекта.
    /// </returns>
    /// <exception cref="ArgumentException">Нет функции или имени; имя не подходит под <see cref="ToolSpec.NamePattern"/>; схема не объект.</exception>
    public static ToolSpec ToSpec(this ToolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        FunctionDefinition function = definition.Function
            ?? throw new ArgumentException("У определения инструмента нет функции.", nameof(definition));

        JsonElement parameters = function.Parameters is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } given
            ? given.Clone()
            : JsonDocument.Parse(NoParametersSchema).RootElement.Clone();

        return new ToolSpec
        {
            Name = function.Name ?? throw new ArgumentException("У функции нет имени.", nameof(definition)),
            Description = function.Description ?? "",
            Parameters = parameters,
            Action = ToolAction.Unknown,
        };
    }

    // ------------------------------------------------------------------
    // Ответ: контракт → старая форма
    // ------------------------------------------------------------------

    /// <summary>Итог контракта в ответ старой формы.</summary>
    /// <param name="answer">Итог порта.</param>
    /// <returns>
    /// Ответ с одним вариантом: <c>finish_reason</c> <c>length</c> для <see cref="LlmStatus.Truncated"/>,
    /// <c>tool_calls</c> при вызовах, иначе <c>stop</c>. Текста нет — пустая строка, как у старого клиента.
    /// </returns>
    /// <exception cref="LlmRequestException">Итог <see cref="LlmStatus.Empty"/> или <see cref="LlmStatus.Failed"/>, см. <see cref="ToException(LlmAnswer)"/>.</exception>
    /// <exception cref="InvalidOperationException">Итог <see cref="LlmStatus.Unknown"/>.</exception>
    public static ChatCompletionsResponse ToResponse(this LlmAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.ToException() is { } error)
            throw error;

        List<LlmToolCall>? calls = answer.ToolCalls is { Count: > 0 } given ? [.. given.Select(ToLlmToolCall)] : null;

        return new ChatCompletionsResponse
        {
            Model = answer.Model,
            Choices =
            [
                new Choice
                {
                    Message = new LLMMessage
                    {
                        Role = LLMMessage.AssistantRole,
                        Content = answer.Text ?? "",
                        Reasoning = answer.Reasoning,
                        ToolCalls = calls,
                    },
                    FinishReason = answer.Status == LlmStatus.Truncated ? "length" : calls != null ? "tool_calls" : "stop",
                },
            ],
            Usage = answer.Usage.ToLlmUsage(),
        };
    }

    /// <summary>Исключение старой формы, которым сообщается итог без ответа.</summary>
    /// <param name="answer">Итог порта.</param>
    /// <returns>
    /// <see langword="null"/> для <see cref="LlmStatus.Ok"/> и <see cref="LlmStatus.Truncated"/>.
    /// <see cref="LlmStatus.Empty"/> — <c>LlmRequestException.NoResponse(EmptyModelAnswerException)</c>, как у
    /// <c>ChatLLMApi</c> (<c>IsEmptyAnswer</c>). <see cref="LlmStatus.Failed"/> — <c>LlmRequestException</c> с
    /// условным кодом HTTP, по которому <see cref="FailureOf(Exception)"/> вернет тот же код сбоя: переполнение —
    /// 400 с признаком переполнения, частота — 429, доступ — 403, отказ — 400, сеть и отмена не вызывающим — без
    /// ответа, прочее и незаданный код — 500. <see cref="LlmStatus.Unknown"/> — <see cref="InvalidOperationException"/>.
    /// </returns>
    public static Exception? ToException(this LlmAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        return answer.Status switch
        {
            LlmStatus.Ok or LlmStatus.Truncated => null,
            LlmStatus.Empty => LlmRequestException.NoResponse(new EmptyModelAnswerException()),
            LlmStatus.Failed => answer.Failure switch
            {
                LlmFailure.ContextOverflow =>
                    Refused(HttpStatusCode.BadRequest, "context_length_exceeded: запрос не помещается в окно модели", answer),
                LlmFailure.RateLimit => Refused(HttpStatusCode.TooManyRequests, "превышен предел частоты запросов", answer),
                LlmFailure.Access => Refused(HttpStatusCode.Forbidden, "нет доступа: ключ, права или баланс", answer),
                LlmFailure.ProviderRefusal => Refused(HttpStatusCode.BadRequest, "провайдер отклонил запрос", answer),
                LlmFailure.Network => LlmRequestException.NoResponse(
                    new HttpRequestException(WithText("порт модели: сбой сети", answer))),
                LlmFailure.Cancelled => LlmRequestException.NoResponse(
                    new OperationCanceledException(WithText("порт модели: запрос отменен не вызывающим", answer))),
                LlmFailure.Stalled => LlmRequestException.NoResponse(
                    new TimeoutException(WithText("порт модели: модель замолчала", answer))),
                _ => Refused(HttpStatusCode.InternalServerError,
                    $"сбой порта модели ({answer.Failure?.ToString() ?? "код не задан"})", answer),
            },
            _ => new InvalidOperationException($"Порт модели вернул итог без статуса ({answer.Status})."),
        };
    }

    /// <summary>Расход контракта в расход старой формы.</summary>
    /// <param name="usage">Расход порта.</param>
    /// <returns>
    /// Расход; <see langword="null"/> — пустой (нули), как у старого клиента, когда провайдер ничего не сообщил.
    /// Стоимость кладется числом JSON, как приходит от провайдера: так ее читает <c>CostExtractor</c>.
    /// </returns>
    public static Usage ToLlmUsage(this ModelUsage? usage)
    {
        if (usage == null)
            return new Usage();

        int input = Clamp(usage.InputTokens);
        int output = Clamp(usage.OutputTokens);

        return new Usage
        {
            PromptTokens = input,
            CompletionTokens = output,
            TotalTokens = Clamp((long)input + output),
            Cost = usage.Cost is decimal cost ? JsonSerializer.SerializeToElement(cost) : null,
        };
    }
}
