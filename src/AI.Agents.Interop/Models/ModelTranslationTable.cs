namespace AI.Agents.Interop.Models;

/// <summary>
/// Явный список перевода между старой формой модели (AI.LLM) и контрактами модели: каждое публичное свойство
/// переводимых типов записано либо в <see cref="Translated"/> (куда идет), либо в <see cref="NotTranslated"/> (почему нет).
/// </summary>
/// <remarks>
/// Ключ — <c>Тип.Свойство</c>, у вложенного типа — <c>Внешний.Вложенный.Свойство</c>. Вызов инструмента есть в обеих
/// формах под одним именем, поэтому старый записан как <c>LLM.ToolCall</c>. Храповик в тестах перебирает свойства
/// рефлексией: новое свойство без записи здесь роняет тест.
/// </remarks>
public static class ModelTranslationTable
{
    /// <summary>Свойства, которые переводятся, и куда.</summary>
    public static IReadOnlyDictionary<string, string> Translated { get; } = new Dictionary<string, string>
    {
        // ModelAsk → LLMMessage + GenerateSettings
        ["ModelAsk.Messages"] = "список LLMMessage по порядку",
        ["ModelAsk.ToolMode"] =
            "GenerateSettings.ToolChoice: Auto → auto, None → none, Required → required, Named → функция ToolName; " +
            "без инструментов Auto и None — без ToolChoice; Unknown, Required и Named без инструментов — ArgumentException",
        ["ModelAsk.Tools"] = "GenerateSettings.Tools через ToolSpec → ToolDefinition; null или пусто → null",
        ["ModelAsk.ToolName"] = "ToolChoice.ForFunction; не из Tools или не при Named — ArgumentException",
        ["ModelAsk.Schema"] = "GenerateSettings.ResponseFormat json_schema с именем «response», strict; null → без формата",
        ["ModelAsk.MaxTokens"] = "GenerateSettings.MaxTokens; null → значение начальных настроек порта",
        ["ModelAsk.Temperature"] = "GenerateSettings.Temperature; null → значение начальных настроек порта",
        ["ModelAsk.Reasoning"] =
            "ReasoningSettings.Effort, если в начальных настройках порта есть ReasoningSettings (OpenRouter), иначе " +
            "GenerateSettings.ReasoningEffort; значение строчными: none, minimal, low, medium, high; null → начальные настройки",

        // ModelMessage → LLMMessage
        ["ModelMessage.Role"] = "LLMMessage.Role: system, user, assistant, tool; Unknown — ArgumentException",
        ["ModelMessage.Parts"] =
            "LLMMessage.Content: текст без кэша — строкой (несколько частей через перенос строки), иначе MessageContent; " +
            "без частей — null у assistant, пустая строка у остальных; картинка или звук не у User — NotSupportedException",
        ["ModelMessage.ToolCalls"] = "LLMMessage.ToolCalls",
        ["ModelMessage.ToolCallId"] = "LLMMessage.ToolCallId",
        ["ModelMessage.Cache"] =
            "CacheControl ephemeral на последней текстовой части; у сообщения без текста — NotSupportedException",

        // MessagePart → IContentItem
        ["MessagePart.Text.Content"] = "TextContentItem.Text или строка LLMMessage.Content",
        ["MessagePart.Image.ContentType"] = "MIME в data:-адресе при Data; при Uri старая форма тип не хранит",
        ["MessagePart.Image.Data"] = "ImageContent с адресом data:{ContentType};base64,…",
        ["MessagePart.Image.Uri"] = "ImageContent.ImageUrl.Url",
        ["MessagePart.Audio.ContentType"] =
            "InputAudio.Format: audio/mpeg → mp3, audio/wav → wav, audio/mp4 → m4a, прочее — подтип; не audio/* — NotSupportedException",
        ["MessagePart.Audio.Data"] = "InputAudio.Data (base64)",
        ["MessagePart.Audio.Uri"] = "в старой форме звук только байтами: звук по адресу — NotSupportedException до отправки",

        // ToolSpec → ToolDefinition
        ["ToolSpec.Name"] = "FunctionDefinition.Name",
        ["ToolSpec.Description"] = "FunctionDefinition.Description",
        ["ToolSpec.Parameters"] = "FunctionDefinition.Parameters",

        // ToolCall контракта ↔ старый
        ["ToolCall.Id"] = "LLM.ToolCall.Id",
        ["ToolCall.Name"] = "FunctionCall.Name",
        ["ToolCall.Arguments"] = "FunctionCall.Arguments: строка JSON (неразобранный текст модели) — как была, иначе текст JSON",
        ["LLM.ToolCall.Id"] = "ToolCall.Id; null → пустая строка (потеря в списке lost)",
        ["LLM.ToolCall.Function"] = "ToolCall.Name и ToolCall.Arguments",
        ["FunctionCall.Name"] = "ToolCall.Name; null → пустая строка",
        ["FunctionCall.Arguments"] =
            "ToolCall.Arguments: пусто → {}, JSON — как есть, не JSON — строка JSON с исходным текстом",

        // ChatCompletionsResponse → LlmAnswer
        ["ChatCompletionsResponse.Choices"] = "первый вариант → LlmAnswer; остальные не переводятся (потеря в списке lost)",
        ["ChatCompletionsResponse.Model"] = "LlmAnswer.Model; пусто → null",
        ["ChatCompletionsResponse.Usage"] = "LlmAnswer.Usage через Usage → ModelUsage",
        ["Choice.Message"] = "текст, рассуждение, вызовы и отказ ответа",
        ["Choice.FinishReason"] = "LlmAnswer.Status: length → Truncated, content_filter → Failed (ProviderRefusal)",
        ["Choice.NativeFinishReason"] = "LlmAnswer.Status: MAX_TOKENS → Truncated",
        ["Choice.Reasoning"] = "LlmAnswer.Reasoning, если его нет в сообщении",
        ["Choice.Refusal"] = "как LLMMessage.Refusal, если его нет в сообщении",

        // LLMMessage → ModelMessage и ответ
        ["LLMMessage.Role"] = "ModelMessage.Role: system, user, assistant, tool без учета регистра; иная — NotSupportedException",
        ["LLMMessage.Content"] =
            "ModelMessage.Parts (строка — одна текстовая часть, MessageContent — по части на элемент; происхождение " +
            "Unknown); в ответе — LlmAnswer.Text, пусто → null",
        ["LLMMessage.ToolCalls"] = "ModelMessage.ToolCalls; в ответе — LlmAnswer.ToolCalls",
        ["LLMMessage.ToolCallId"] = "ModelMessage.ToolCallId",
        ["LLMMessage.Reasoning"] = "в ответе — LlmAnswer.Reasoning; в истории не переводится (потеря в списке lost)",
        ["LLMMessage.Refusal"] =
            "в ответе без текста и вызовов — Failed (ProviderRefusal), текст отказа — LlmAnswer.FailureText; отказ рядом " +
            "с ответом — потеря в списке lost",

        // Usage ↔ ModelUsage
        ["Usage.PromptTokens"] = "ModelUsage.InputTokens",
        ["Usage.CompletionTokens"] = "ModelUsage.OutputTokens",
        ["Usage.Cost"] = "ModelUsage.Cost: JSON — через CostExtractor, число CLR — как есть; обратно — число JSON",
        ["Usage.TotalTokens"] = "вычисляется: обратно InputTokens + OutputTokens",
        ["ModelUsage.InputTokens"] = "Usage.PromptTokens (больше int.MaxValue — int.MaxValue)",
        ["ModelUsage.OutputTokens"] = "Usage.CompletionTokens",
        ["ModelUsage.Cost"] = "Usage.Cost числом JSON",

        // LlmAnswer → ChatCompletionsResponse
        ["LlmAnswer.Status"] =
            "Ok → finish_reason stop или tool_calls, Truncated → length; Empty → LlmRequestException.NoResponse" +
            "(EmptyModelAnswerException); Failed → LlmRequestException по Failure; Unknown — InvalidOperationException",
        ["LlmAnswer.Text"] = "LLMMessage.Content; null → пустая строка",
        ["LlmAnswer.Reasoning"] = "LLMMessage.Reasoning",
        ["LlmAnswer.ToolCalls"] = "LLMMessage.ToolCalls",
        ["LlmAnswer.Failure"] =
            "условный код LlmRequestException: ContextOverflow — 400 с признаком переполнения, RateLimit — 429, " +
            "Access — 403, ProviderRefusal — 400, Network и Cancelled — без ответа, Other и null — 500",
        ["LlmAnswer.FailureText"] =
            "дописывается к сообщению LlmRequestException после причины; вперед — текст исключения клиента, отказ " +
            "модели (refusal) или причина content_filter",
        ["LlmAnswer.Model"] = "ChatCompletionsResponse.Model",
        ["LlmAnswer.Usage"] = "ChatCompletionsResponse.Usage; null → нули, как у старого клиента",

        // LlmRequestException → LlmAnswer
        ["LlmRequestException.StatusCode"] =
            "LlmFailure: нет ответа и 408 → Network, 429 → RateLimit, 401/402/403 → Access, " +
            "400/404/409/413/422/451 → ProviderRefusal, прочее → Other",
        ["LlmRequestException.IsContextOverflow"] = "LlmFailure.ContextOverflow",
        ["LlmRequestException.IsEmptyAnswer"] = "LlmStatus.Empty",

        // GenerateSettings → ModelAsk
        ["GenerateSettings.MaxTokens"] = "ModelAsk.MaxTokens",
        ["GenerateSettings.Temperature"] = "ModelAsk.Temperature",
        ["GenerateSettings.ReasoningEffort"] =
            "ModelAsk.Reasoning, если ReasoningSettings.Effort не задан; незнакомое значение — потеря в списке lost",
        ["GenerateSettings.Tools"] = "ModelAsk.Tools через ToolDefinition → ToolSpec (Action = Unknown)",
        ["GenerateSettings.ToolChoice"] =
            "ModelAsk.ToolMode и ToolName; null → Auto при инструментах, иначе None",
        ["GenerateSettings.ResponseFormat"] = "ModelAsk.Schema только для json_schema; json_object — потеря в списке lost",
        ["ToolChoice.Mode"] = "ModelAsk.ToolMode",
        ["ToolChoice.FunctionName"] = "ModelAsk.ToolName",
        ["ToolChoice.IsStringMode"] = "false → ToolMode.Named",
        ["ResponseFormat.Type"] = "json_schema → ModelAsk.Schema; другие — потеря в списке lost",
        ["ResponseFormat.JsonSchema"] = "ModelAsk.Schema",
        ["JsonSchemaDefinition.Schema"] = "ModelAsk.Schema",
        ["JsonSchemaDefinition.Strict"] = "контракт всегда строгий; Strict = false — потеря в списке lost",

        // ToolDefinition → ToolSpec
        ["ToolDefinition.Function"] = "ToolSpec",
        ["FunctionDefinition.Name"] = "ToolSpec.Name (проверка ToolSpec.NamePattern)",
        ["FunctionDefinition.Description"] = "ToolSpec.Description; null → пустая строка",
        ["FunctionDefinition.Parameters"] = "ToolSpec.Parameters; null → пустая схема объекта",

        // Части содержимого → MessagePart
        ["TextContentItem.Text"] = "MessagePart.Text.Content; null → пустая строка",
        ["TextContentItem.CacheControl"] = "ModelMessage.Cache = true",
        ["ImageContent.ImageUrl"] =
            "data:{mime};base64 — MessagePart.Image с Data и ContentType; иначе Uri и ContentType по расширению, " +
            "без него image/*",
        ["AudioContent.InputAudio"] = "MessagePart.Audio",
        ["InputAudio.Data"] = "MessagePart.Audio.Data (из base64)",
        ["InputAudio.Format"] = "MessagePart.Audio.ContentType: mp3 → audio/mpeg, m4a → audio/mp4, прочее — audio/{формат}",

        // Кадры потока
        ["LlmChunk.Text.Chunk"] = "LLMStreamChunk.Content",
        ["LlmChunk.Reasoning.Chunk"] = "LLMStreamChunk.Reasoning",
        ["LlmChunk.Final.Answer"] = "ответ SendFullAsync; клиенту в onChunk итоговый кадр не передается",
        ["LLMStreamChunk.Content"] = "LlmChunk.Text",
        ["LLMStreamChunk.Reasoning"] = "LlmChunk.Reasoning",
    };

    /// <summary>Свойства, которые не переводятся, и почему.</summary>
    public static IReadOnlyDictionary<string, string> NotTranslated { get; } = new Dictionary<string, string>
    {
        ["ModelAsk.Role"] =
            "порт над одним клиентом ставит одну модель на все роли; обратно роль задается при создании клиента",
        ["MessagePart.Text.Origin"] =
            "в старой форме происхождения нет; внешний текст уходит без обрамления (решение владельца, формат — Ф5); " +
            "обратно — OriginKind.Unknown",
        ["MessagePart.Image.Origin"] = "то же, что MessagePart.Text.Origin",
        ["MessagePart.Audio.Origin"] = "то же, что MessagePart.Text.Origin",
        ["ToolCall.EffectKey"] = "в старой форме ключа действия нет; модель его не присылает",
        ["ToolSpec.Action"] = "модели не нужен; обратно — ToolAction.Unknown",
        ["ToolSpec.Idempotency"] = "модели не нужен; обратно — null (не объявлено)",
        ["ToolSpec.Group"] = "модели не передается",
        ["ToolSpec.About"] = "модели не передается: у FunctionDefinition есть только описание",

        ["ChatCompletionsResponse.Id"] = "в LlmAnswer нет идентификатора ответа",
        ["ChatCompletionsResponse.Provider"] = "в LlmAnswer нет провайдера; есть только Model",
        ["ChatCompletionsResponse.Object"] = "служебное поле протокола",
        ["ChatCompletionsResponse.Created"] = "в LlmAnswer нет времени ответа",
        ["Choice.Index"] = "переводится только первый вариант",
        ["Choice.Logprobs"] = "в LlmAnswer нет вероятностей токенов",
        ["Choice.ReasoningDetails"] = "в LlmAnswer рассуждение только текстом",

        ["LLMMessage.Images"] = "картинки в ответе модели (генерация) в LlmAnswer не помещаются; потеря в списке lost",
        ["LLMMessage.Name"] = "в ModelMessage нет имени автора; потеря в списке lost",

        ["Usage.ReasoningTokens"] = "в ModelUsage рассуждение входит в OutputTokens",
        ["Usage.CachedTokens"] = "в ModelUsage нет токенов кэша",

        ["LlmRequestException.IsRetryable"] = "повторы — дело реализации порта; в LlmAnswer признака нет",
        ["LlmRequestException.RetryAfter"] = "в LlmAnswer нет срока повтора",

        ["GenerateSettings.TopP"] = "в ModelAsk нет; вперед — из начальных настроек порта, обратно — потеря в списке lost",
        ["GenerateSettings.TopK"] = "то же, что TopP",
        ["GenerateSettings.RepetitionPenalty"] = "то же, что TopP",
        ["GenerateSettings.MinTokens"] = "то же, что TopP",
        ["GenerateSettings.LogProbs"] = "то же, что TopP",
        ["GenerateSettings.TopLogprobs"] = "то же, что TopP",
        ["GenerateSettings.StreamId"] = "поток в контракте — StreamAsync, а не настройка",
        ["GenerateSettings.StreamMethod"] = "то же, что StreamId",
        ["GenerateSettings.Stream"] = "то же, что StreamId",
        ["GenerateSettings.ReasoningSettings"] =
            "обратно переводится только Effort (ModelAsk.Reasoning); MaxTokens, Exclude, Enabled и незнакомый Effort — " +
            "потеря в списке lost",
        ["GenerateSettings.Modalities"] = "то же, что TopP",
        ["GenerateSettings.IncludeUsage"] = "то же, что TopP; для стоимости у OpenRouter задается в начальных настройках порта",
        ["GenerateSettings.IncludeReasoning"] = "то же, что TopP",
        ["JsonSchemaDefinition.Name"] = "в контракте у схемы нет имени; вперед — «response»",
        ["LLM.ToolCall.Type"] = "всегда function",
        ["LLM.ToolCall.Index"] = "номер в потоке; клиент собирает вызовы сам",
        ["ToolDefinition.Type"] = "всегда function",
        ["TextContentItem.Type"] = "вид части задает тип MessagePart",
        ["ImageContent.Type"] = "вид части задает тип MessagePart",
        ["AudioContent.Type"] = "вид части задает тип MessagePart",

        ["LLMStreamChunk.Provider"] = "фрагменты только для показа; итог несет LlmChunk.Final",
        ["LLMStreamChunk.Model"] = "то же, что Provider",
        ["LLMStreamChunk.FinishReason"] = "то же, что Provider",
        ["LLMStreamChunk.NativeFinishReason"] = "то же, что Provider",
        ["LLMStreamChunk.Usage"] = "расход приходит в итоге (LlmChunk.Final.Answer.Usage)",
        ["LLMStreamChunk.Images"] = "то же, что Provider",
        ["LLMStreamChunk.ToolCalls"] = "вызовы приходят в итоге",
        ["LLMStreamChunk.HasText"] = "вычисляется",
    };
}
