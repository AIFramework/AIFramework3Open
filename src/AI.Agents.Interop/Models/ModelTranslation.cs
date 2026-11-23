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
public static partial class ModelTranslation
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

        if (ask.Temperature is double temperature)
            settings.Temperature = temperature;

        if (ask.Reasoning is ReasoningLevel level)
            SetReasoning(settings, level);

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

        string? failureText = null;
        if (status == LlmStatus.Failed)
        {
            if (text != null) lost?.Add("LLMMessage.Content");
            if (calls != null) lost?.Add("LLMMessage.ToolCalls");
            failureText = refusal ?? "провайдер не отдал ответ: сработал фильтр содержимого (content_filter)";
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
            FailureText = failureText,
            Model = NullIfEmpty(response.Model),
            Usage = response.Usage.ToUsage(),
        };
    }

    /// <summary>Исключение клиента старой формы в итог контракта.</summary>
    /// <param name="error">Что бросил клиент. Отмену токеном вызывающего сюда не передают: она остается исключением.</param>
    /// <returns>
    /// <see cref="LlmStatus.Empty"/> для пустого ответа (<c>EmptyModelAnswerException</c>, в том числе внутри
    /// <c>LlmRequestException</c>); иначе <see cref="LlmStatus.Failed"/> с кодом из <see cref="FailureOf(Exception)"/>
    /// и текстом исключения в <see cref="LlmAnswer.FailureText"/>.
    /// </returns>
    public static LlmAnswer ToAnswer(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error is EmptyModelAnswerException or LlmRequestException { IsEmptyAnswer: true })
            return new LlmAnswer { Status = LlmStatus.Empty };

        return new LlmAnswer
        {
            Status = LlmStatus.Failed,
            Failure = FailureOf(error),
            FailureText = NullIfEmpty(error.Message),
        };
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
}
