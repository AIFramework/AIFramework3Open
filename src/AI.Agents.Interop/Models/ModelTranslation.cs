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

/// <summary>
/// Перевод значений между старой формой модели (<c>LLMMessage</c>, <c>GenerateSettings</c>,
/// <c>ChatCompletionsResponse</c>, <c>LlmRequestException</c>) и контрактами (<see cref="ModelAsk"/>,
/// <see cref="ModelMessage"/>, <see cref="LlmAnswer"/>).
/// </summary>
/// <remarks>
/// <para>
/// Куда идет каждое поле и что не переводится, записано в <see cref="ModelTranslationTable"/>. Методы, которые
/// теряют поле без исключения, складывают его ключ из таблицы в список <c>lost</c>: о потере решает вызывающий
/// (адаптеры пишут предупреждение в журнал), но молча она не проходит.
/// </para>
/// <para>
/// Запрос, который старая форма не может выразить (звук по адресу, картинка не в сообщении пользователя,
/// <see cref="ToolMode.Unknown"/>), отвергается исключением до отправки: это ошибка вызывающего, а не сбой модели.
/// </para>
/// </remarks>
public static class ModelTranslation
{
    /// <summary>Имя схемы ответа в <c>ResponseFormat</c>: в контракте у схемы имени нет, а старой форме оно нужно.</summary>
    public const string SchemaName = "response";

    /// <summary>Схема параметров инструмента без параметров: старая форма разрешает <c>null</c>, контракт — только объект.</summary>
    private const string NoParametersSchema = """{"type":"object","properties":{}}""";

    // ------------------------------------------------------------------
    // Запрос: контракт → старая форма
    // ------------------------------------------------------------------

    /// <summary>Сообщения контракта в сообщения старой формы, по порядку.</summary>
    /// <param name="messages">Сообщения контракта.</param>
    /// <returns>Сообщения старой формы.</returns>
    /// <exception cref="ArgumentException">Роль не задана, у картинки нет ни байтов, ни адреса.</exception>
    /// <exception cref="NotSupportedException">Часть, которую старая форма не выражает (см. <see cref="ToLlmMessage"/>).</exception>
    public static List<LLMMessage> ToLlmMessages(IEnumerable<ModelMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return [.. messages.Select(ToLlmMessage)];
    }

    /// <summary>Сообщение контракта в сообщение старой формы.</summary>
    /// <param name="message">Сообщение контракта.</param>
    /// <returns>
    /// Сообщение старой формы. Текст без кэша идет строкой (несколько частей — через перенос строки, как склеивает
    /// старая форма); с картинками, звуком или меткой кэша — частями. Происхождение частей не переводится: в старой
    /// форме его нет, внешний текст уходит как есть.
    /// </returns>
    /// <exception cref="ArgumentException">Роль <see cref="MessageRole.Unknown"/>; у картинки нет ни байтов, ни адреса или есть оба.</exception>
    /// <exception cref="NotSupportedException">
    /// Картинка или звук не в сообщении пользователя (старая форма их там выбрасывает); звук по адресу (в старой форме
    /// звук только байтами); метка кэша у сообщения без текста (старая форма ставит ее только на текстовую часть).
    /// </exception>
    public static LLMMessage ToLlmMessage(this ModelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        string role = message.Role switch
        {
            MessageRole.System => LLMMessage.SystemRole,
            MessageRole.User => LLMMessage.UserRole,
            MessageRole.Assistant => LLMMessage.AssistantRole,
            MessageRole.Tool => LLMMessage.ToolRole,
            _ => throw new ArgumentException($"Роль сообщения не задана ({message.Role}).", nameof(message)),
        };

        IReadOnlyList<MessagePart> parts = message.Parts ?? [];
        bool user = message.Role == MessageRole.User;

        if (!user && parts.FirstOrDefault(p => p is not MessagePart.Text) is { } media)
            throw new NotSupportedException(
                $"{media.GetType().Name} в сообщении {message.Role}: старая форма принимает картинки и звук только в " +
                "сообщении пользователя и молча выбрасывает их из остальных. Перенесите их в сообщение User.");

        object? content;
        if (parts.Count == 0)
        {
            if (message.Cache)
                throw new NotSupportedException(
                    "Метка кэша у сообщения без частей: старая форма ставит метку только на текстовую часть.");

            content = message.Role == MessageRole.Assistant ? null : "";
        }
        else if (!message.Cache && parts.All(p => p is MessagePart.Text) && (parts.Count == 1 || !user))
        {
            content = string.Join("\n", parts.Cast<MessagePart.Text>().Select(t => t.Content));
        }
        else
        {
            var items = new MessageContent();
            foreach (MessagePart part in parts)
                items.Add(ToContentItem(part));

            if (message.Cache)
            {
                TextContentItem last = items.OfType<TextContentItem>().LastOrDefault()
                    ?? throw new NotSupportedException(
                        "Метка кэша у сообщения без текста: старая форма ставит метку только на текстовую часть.");
                last.CacheControl = CacheControl.Ephemeral;
            }

            content = items;
        }

        return new LLMMessage
        {
            Role = role,
            Content = content,
            ToolCalls = message.ToolCalls is { Count: > 0 } calls ? [.. calls.Select(ToLlmToolCall)] : null,
            ToolCallId = message.ToolCallId,
        };
    }

    /// <summary>Настройки старой формы для запроса контракта.</summary>
    /// <param name="ask">Запрос.</param>
    /// <param name="baseSettings">
    /// Настройки, с которых начинается запрос (температура, рассуждение и прочее, чего в контракте нет); копируются,
    /// исходные не меняются. <see langword="null"/>: <c>new GenerateSettings()</c>. Инструменты, выбор инструмента и
    /// формат ответа всегда берутся из запроса; предел токенов — из запроса, если он там задан.
    /// </param>
    /// <returns>Настройки для <c>ILLMClient.SendFullAsync</c>.</returns>
    /// <exception cref="ArgumentException">
    /// <see cref="ToolMode.Unknown"/>; <see cref="ToolMode.Required"/> или <see cref="ToolMode.Named"/> без
    /// инструментов; <see cref="ModelAsk.ToolName"/> не при <see cref="ToolMode.Named"/> или не из списка.
    /// </exception>
    public static GenerateSettings ToSettings(this ModelAsk ask, GenerateSettings? baseSettings = null)
    {
        ArgumentNullException.ThrowIfNull(ask);

        GenerateSettings settings = baseSettings?.Clone() ?? new GenerateSettings();
        settings.Tools = ask.Tools is { Count: > 0 } tools ? [.. tools.Select(ToDefinition)] : null;

        if (ask.ToolName != null && ask.ToolMode != ToolMode.Named)
            throw new ArgumentException($"ToolName задан при ToolMode.{ask.ToolMode}; он нужен только при Named.", nameof(ask));

        settings.ToolChoice = ask.ToolMode switch
        {
            ToolMode.Auto => settings.Tools == null ? null : ToolChoice.Auto(),
            ToolMode.None => settings.Tools == null ? null : ToolChoice.None(),
            ToolMode.Required => settings.Tools != null
                ? ToolChoice.Required()
                : throw new ArgumentException("ToolMode.Required без инструментов.", nameof(ask)),
            ToolMode.Named => Named(ask),
            _ => throw new ArgumentException($"ToolMode не задан ({ask.ToolMode}).", nameof(ask)),
        };

        settings.ResponseFormat = ask.Schema is { } schema
            ? ResponseFormat.CreateJsonSchema(SchemaName, schema.GetRawText())
            : null;

        if (ask.MaxTokens is int maxTokens)
            settings.MaxTokens = maxTokens;

        return settings;
    }

    /// <summary>Описание инструмента контракта в определение функции старой формы.</summary>
    /// <param name="spec">Описание инструмента.</param>
    /// <returns>Определение для <c>GenerateSettings.Tools</c>. Вид действия, группа и <c>About</c> модели не передаются.</returns>
    public static ToolDefinition ToDefinition(this ToolSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return new ToolDefinition
        {
            Function = new FunctionDefinition
            {
                Name = spec.Name,
                Description = spec.Description,
                Parameters = spec.Parameters.Clone(),
            },
        };
    }

    /// <summary>Вызов инструмента контракта в вызов старой формы.</summary>
    /// <param name="call">Вызов контракта.</param>
    /// <returns>
    /// Вызов старой формы. Аргументы-строка JSON — это неразобранный текст модели (<see cref="ContractToolCall.Arguments"/>),
    /// он уходит как был; остальное — текстом JSON. <c>EffectKey</c> не переводится.
    /// </returns>
    public static LlmToolCall ToLlmToolCall(this ContractToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return new LlmToolCall
        {
            Id = call.Id,
            Function = new FunctionCall
            {
                Name = call.Name,
                Arguments = call.Arguments.ValueKind == JsonValueKind.String
                    ? call.Arguments.GetString()
                    : call.Arguments.GetRawText(),
            },
        };
    }

    // ------------------------------------------------------------------
    // Ответ: старая форма → контракт
    // ------------------------------------------------------------------

    /// <summary>Ответ старой формы в итог контракта.</summary>
    /// <param name="response">Ответ клиента.</param>
    /// <param name="lost">Куда сложить ключи непереведенных полей; <see langword="null"/>: не собирать.</param>
    /// <returns>
    /// Итог по первому варианту ответа. <c>finish_reason = length</c> или <c>MAX_TOKENS</c> — <see cref="LlmStatus.Truncated"/>;
    /// <c>content_filter</c> или отказ модели (<c>refusal</c>) без ответа — <see cref="LlmStatus.Failed"/> с
    /// <see cref="LlmFailure.ProviderRefusal"/>; ни текста, ни вызовов — <see cref="LlmStatus.Empty"/>; иначе
    /// <see cref="LlmStatus.Ok"/>.
    /// </returns>
    public static LlmAnswer ToAnswer(this ChatCompletionsResponse response, ICollection<string>? lost = null)
    {
        ArgumentNullException.ThrowIfNull(response);

        Choice? choice = response.Choices?.FirstOrDefault();
        LLMMessage? message = choice?.Message;

        if (response.Choices is { Count: > 1 })
            lost?.Add("ChatCompletionsResponse.Choices");

        string? text = Text(message?.Content);
        string? reasoning = NullIfEmpty(message?.Reasoning) ?? NullIfEmpty(choice?.Reasoning);
        string? refusal = NullIfEmpty(message?.Refusal) ?? NullIfEmpty(choice?.Refusal);
        IReadOnlyList<ContractToolCall>? calls = message?.ToolCalls is { Count: > 0 } raw
            ? [.. raw.Select(c => ToToolCall(c, lost))]
            : null;

        if (message?.Images is { Count: > 0 })
            lost?.Add("LLMMessage.Images");

        LlmStatus status;
        LlmFailure? failure = null;

        if (string.Equals(choice?.FinishReason, "content_filter", StringComparison.OrdinalIgnoreCase)
            || (refusal != null && text == null && calls == null))
        {
            status = LlmStatus.Failed;
            failure = LlmFailure.ProviderRefusal;
        }
        else if (string.Equals(choice?.FinishReason, "length", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(choice?.NativeFinishReason, "MAX_TOKENS", StringComparison.OrdinalIgnoreCase))
        {
            status = LlmStatus.Truncated;
        }
        else
        {
            status = text == null && calls == null ? LlmStatus.Empty : LlmStatus.Ok;
        }

        if (status == LlmStatus.Failed)
        {
            if (text != null) lost?.Add("LLMMessage.Content");
            if (calls != null) lost?.Add("LLMMessage.ToolCalls");
            if (refusal != null) lost?.Add("LLMMessage.Refusal");
            text = null;
            calls = null;
        }
        else if (refusal != null)
        {
            lost?.Add("LLMMessage.Refusal");
        }

        return new LlmAnswer
        {
            Status = status,
            Text = text,
            Reasoning = reasoning,
            ToolCalls = calls,
            Failure = failure,
            Model = NullIfEmpty(response.Model),
            Usage = response.Usage.ToUsage(),
        };
    }

    /// <summary>Исключение клиента старой формы в итог контракта.</summary>
    /// <param name="error">Что бросил клиент. Отмену токеном вызывающего сюда не передают: она остается исключением.</param>
    /// <returns>
    /// <see cref="LlmStatus.Empty"/> для пустого ответа (<c>EmptyModelAnswerException</c>, в том числе внутри
    /// <c>LlmRequestException</c>); иначе <see cref="LlmStatus.Failed"/> с кодом из <see cref="FailureOf(Exception)"/>.
    /// Текст сбоя для человека в контракте не помещается — его пишет в журнал адаптер.
    /// </returns>
    public static LlmAnswer ToAnswer(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error is EmptyModelAnswerException or LlmRequestException { IsEmptyAnswer: true })
            return new LlmAnswer { Status = LlmStatus.Empty };

        return new LlmAnswer { Status = LlmStatus.Failed, Failure = FailureOf(error) };
    }

    /// <summary>Код сбоя по исключению клиента.</summary>
    /// <param name="error">Исключение.</param>
    /// <returns>
    /// Для <c>LlmRequestException</c>: переполнение окна — <see cref="LlmFailure.ContextOverflow"/>; ответа нет (сеть,
    /// обрыв потока) или 408 — <see cref="LlmFailure.Network"/>; 429 — <see cref="LlmFailure.RateLimit"/>; 401, 402,
    /// 403 — <see cref="LlmFailure.Access"/>; 400, 404, 409, 413, 422, 451 — <see cref="LlmFailure.ProviderRefusal"/>;
    /// прочее — <see cref="LlmFailure.Other"/>. Сеть, тайм-аут и ввод-вывод вне <c>LlmRequestException</c> —
    /// <see cref="LlmFailure.Network"/>, остальное — <see cref="LlmFailure.Other"/>.
    /// </returns>
    public static LlmFailure FailureOf(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error switch
        {
            LlmRequestException { IsContextOverflow: true } => LlmFailure.ContextOverflow,
            LlmRequestException { StatusCode: null } => LlmFailure.Network,
            LlmRequestException { StatusCode: { } code } => (int)code switch
            {
                408 => LlmFailure.Network,
                429 => LlmFailure.RateLimit,
                401 or 402 or 403 => LlmFailure.Access,
                400 or 404 or 409 or 413 or 422 or 451 => LlmFailure.ProviderRefusal,
                _ => LlmFailure.Other,
            },
            HttpRequestException or TimeoutException or IOException or OperationCanceledException => LlmFailure.Network,
            _ => LlmFailure.Other,
        };
    }

    /// <summary>Расход старой формы в расход контракта.</summary>
    /// <param name="usage">Расход из ответа.</param>
    /// <returns>
    /// Расход; <see langword="null"/>, если его нет или в нем одни нули без стоимости: старая форма заполняет
    /// пустой расход нулями, когда провайдер ничего не сообщил. Стоимость из JSON провайдера разбирает
    /// <c>CostExtractor</c>, как в <c>AgentUsage</c>; число CLR берется как есть. Токены рассуждения и кэша
    /// контракт отдельно не несет.
    /// </returns>
    public static ModelUsage? ToUsage(this Usage? usage)
    {
        if (usage == null)
            return null;

        decimal? cost = Cost(usage.Cost);
        if (usage.PromptTokens == 0 && usage.CompletionTokens == 0 && cost == null)
            return null;

        return new ModelUsage { InputTokens = usage.PromptTokens, OutputTokens = usage.CompletionTokens, Cost = cost };
    }

    /// <summary>Вызов инструмента старой формы в вызов контракта.</summary>
    /// <param name="call">Вызов старой формы.</param>
    /// <returns>Вызов контракта; аргументы разбираются по <see cref="ParseArguments(string?)"/>, нет имени или
    /// идентификатора — пустая строка.</returns>
    public static ContractToolCall ToToolCall(this LlmToolCall call) => ToToolCall(call, lost: null);

    /// <summary>Аргументы вызова старой формы (текст) в аргументы контракта (JSON).</summary>
    /// <param name="arguments">Текст аргументов от модели.</param>
    /// <returns>
    /// Разобранный JSON; пусто — пустой объект (функция без параметров); не JSON — строка JSON с исходным текстом,
    /// как требует <see cref="ContractToolCall.Arguments"/>.
    /// </returns>
    public static JsonElement ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return JsonSerializer.SerializeToElement(new Dictionary<string, object>());

        try
        {
            using JsonDocument document = JsonDocument.Parse(arguments);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(arguments);
        }
    }

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
                    Refused(HttpStatusCode.BadRequest, "context_length_exceeded: запрос не помещается в окно модели"),
                LlmFailure.RateLimit => Refused(HttpStatusCode.TooManyRequests, "превышен предел частоты запросов"),
                LlmFailure.Access => Refused(HttpStatusCode.Forbidden, "нет доступа: ключ, права или баланс"),
                LlmFailure.ProviderRefusal => Refused(HttpStatusCode.BadRequest, "провайдер отклонил запрос"),
                LlmFailure.Network => LlmRequestException.NoResponse(new HttpRequestException("порт модели: сбой сети")),
                LlmFailure.Cancelled => LlmRequestException.NoResponse(
                    new OperationCanceledException("порт модели: запрос отменен не вызывающим")),
                _ => Refused(HttpStatusCode.InternalServerError, $"сбой порта модели ({answer.Failure?.ToString() ?? "код не задан"})"),
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

    // ------------------------------------------------------------------
    // Внутреннее
    // ------------------------------------------------------------------

    private static readonly Origin UnknownOrigin = new() { Kind = OriginKind.Unknown };

    private static ToolChoice Named(ModelAsk ask)
    {
        if (string.IsNullOrEmpty(ask.ToolName))
            throw new ArgumentException("ToolMode.Named без ToolName.", nameof(ask));

        if (ask.Tools?.Any(t => ToolSpec.NameComparer.Equals(t.Name, ask.ToolName)) != true)
            throw new ArgumentException($"ToolMode.Named: инструмента «{ask.ToolName}» нет в Tools.", nameof(ask));

        return ToolChoice.ForFunction(ask.ToolName);
    }

    private static IContentItem ToContentItem(MessagePart part) => part switch
    {
        MessagePart.Text text => new TextContentItem(text.Content),
        MessagePart.Image { Data: { } data, Uri: null } image =>
            new ImageContent($"data:{image.ContentType};base64,{Convert.ToBase64String(data)}"),
        MessagePart.Image { Data: null, Uri: { } uri } => new ImageContent(uri),
        MessagePart.Image => throw new ArgumentException("У картинки должны быть байты (Data) или адрес (Uri), ровно одно из двух."),
        MessagePart.Audio { Data: { } data } audio => new AudioContent(Convert.ToBase64String(data), AudioFormat(audio.ContentType)),
        MessagePart.Audio => throw new NotSupportedException("Звук по адресу: в старой форме звук передается только байтами."),
        _ => throw new NotSupportedException($"Часть сообщения {part.GetType().Name} старая форма не выражает."),
    };

    private static MessagePart ToPart(IContentItem item, ref bool cache)
    {
        switch (item)
        {
            case TextContentItem text:
                cache |= text.CacheControl != null;
                return new MessagePart.Text { Content = text.Text ?? "", Origin = UnknownOrigin };

            case ImageContent { ImageUrl.Url: { } url }:
                if (TryParseDataUrl(url, out string? mime, out byte[]? bytes))
                    return new MessagePart.Image { ContentType = mime!, Data = bytes, Origin = UnknownOrigin };
                return new MessagePart.Image { ContentType = ImageTypeByAddress(url), Uri = url, Origin = UnknownOrigin };

            case AudioContent { InputAudio: { Data: { } data } audio }:
                return new MessagePart.Audio
                {
                    ContentType = AudioContentType(audio.Format),
                    Data = Convert.FromBase64String(data),
                    Origin = UnknownOrigin,
                };

            default:
                throw new NotSupportedException($"Часть содержимого {item?.GetType().Name ?? "null"} в контракте не выражается.");
        }
    }

    private static ContractToolCall ToToolCall(LlmToolCall call, ICollection<string>? lost)
    {
        ArgumentNullException.ThrowIfNull(call);

        if (call.Id == null) lost?.Add("ToolCall.Id");

        return new ContractToolCall
        {
            Id = call.Id ?? "",
            Name = call.Function?.Name ?? "",
            Arguments = ParseArguments(call.Function?.Arguments),
        };
    }

    private static void CollectLostSettings(GenerateSettings s, ICollection<string> lost)
    {
        if (s.Temperature != null) lost.Add("GenerateSettings.Temperature");
        if (s.TopP != null) lost.Add("GenerateSettings.TopP");
        if (s.TopK != null) lost.Add("GenerateSettings.TopK");
        if (s.RepetitionPenalty != null) lost.Add("GenerateSettings.RepetitionPenalty");
        if (s.MinTokens != null) lost.Add("GenerateSettings.MinTokens");
        if (s.LogProbs != null) lost.Add("GenerateSettings.LogProbs");
        if (s.TopLogprobs != null) lost.Add("GenerateSettings.TopLogprobs");
        if (s.Stream) lost.Add("GenerateSettings.StreamId");
        if (s.ReasoningSettings != null) lost.Add("GenerateSettings.ReasoningSettings");
        if (s.ReasoningEffort != null) lost.Add("GenerateSettings.ReasoningEffort");
        if (s.Modalities is { Count: > 0 }) lost.Add("GenerateSettings.Modalities");
        if (s.IncludeUsage != null) lost.Add("GenerateSettings.IncludeUsage");
        if (s.IncludeReasoning != null) lost.Add("GenerateSettings.IncludeReasoning");
    }

    /// <summary>
    /// Стоимость: и число CLR, и JSON провайдера (<see cref="JsonElement"/>) разбирает <c>CostExtractor</c>
    /// без оглядки на культуру процесса.
    /// </summary>
    private static decimal? Cost(object? cost) => cost is null ? null : CostExtractor.TryExtract(cost);

    private static LlmRequestException Refused(HttpStatusCode code, string reason) =>
        LlmRequestException.FromResponse(
            code, JsonSerializer.Serialize(new { error = new { message = "порт модели: " + reason } }), retryAfter: null);

    private static string? Text(object? content) => content switch
    {
        null => null,
        string text => NullIfEmpty(text),
        MessageContent items => NullIfEmpty(items.ToString()),
        var other => NullIfEmpty(other.ToString()),
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static int Clamp(long value) => (int)Math.Clamp(value, 0, int.MaxValue);

    /// <summary>Формат <c>input_audio</c> по типу содержимого: <c>audio/wav</c> → <c>wav</c>, <c>audio/mpeg</c> → <c>mp3</c>.</summary>
    private static string AudioFormat(string contentType)
    {
        const string prefix = "audio/";
        if (!contentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Тип звука «{contentType}» не audio/*: формат для старой формы не определить.");

        string subtype = contentType[prefix.Length..].Split(';')[0].Trim().ToLowerInvariant();
        if (subtype.StartsWith("x-", StringComparison.Ordinal))
            subtype = subtype[2..];

        return subtype switch
        {
            "mpeg" or "mp3" => "mp3",
            "wave" or "wav" => "wav",
            "mp4" or "m4a" => "m4a",
            "" => throw new NotSupportedException($"Тип звука «{contentType}» без подтипа."),
            _ => subtype,
        };
    }

    /// <summary>Тип содержимого по формату <c>input_audio</c>: обратное к <see cref="AudioFormat(string)"/>.</summary>
    private static string AudioContentType(string? format) => format?.ToLowerInvariant() switch
    {
        "mp3" => "audio/mpeg",
        "m4a" => "audio/mp4",
        null or "" => throw new NotSupportedException("У звука не указан формат."),
        var other => "audio/" + other,
    };

    private static bool TryParseDataUrl(string url, out string? mime, out byte[]? bytes)
    {
        mime = null;
        bytes = null;

        const string marker = ";base64,";
        if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

        int at = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
            return false;

        mime = url[5..at];
        bytes = Convert.FromBase64String(url[(at + marker.Length)..]);
        return mime.Length > 0;
    }

    /// <summary>
    /// Тип картинки по адресу: по расширению, иначе <c>image/*</c> — «картинка, подтип неизвестен». Старая форма
    /// тип картинки по адресу не хранит, а контракт требует его.
    /// </summary>
    private static string ImageTypeByAddress(string url)
    {
        string path = url.Split('?', '#')[0];
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/*",
        };
    }
}
