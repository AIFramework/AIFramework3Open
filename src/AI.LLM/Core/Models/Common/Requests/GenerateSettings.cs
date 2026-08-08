namespace AI.LLM.Core.Models.Common.Requests;

/// <summary>
/// Представляет настройки конфигурации для генерации текста.
/// </summary>
public class GenerateSettings
{
    #region Настройки семплирования

    /// <summary>
    /// Температура (0.0-2.0). Выше = креативнее, ниже = сфокусированнее.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Nucleus sampling (0.0-1.0). Рассматриваются токены с суммарной вероятностью TopP.
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// Количество топовых токенов для семплирования (> 0).
    /// </summary>
    public int? TopK { get; set; }

    #endregion

    #region Настройки повторений и длины

    /// <summary>
    /// Штраф за повторения (1.0-2.0).
    /// </summary>
    public double? RepetitionPenalty { get; set; }

    /// <summary>
    /// Минимальное количество токенов (≥ 0).
    /// </summary>
    public int? MinTokens { get; set; }

    /// <summary>
    /// Максимальное количество токенов (> 0).
    /// </summary>
    public int? MaxTokens { get; set; }

    #endregion

    #region Настройки логирования

    /// <summary>
    /// Выводить ли логарифмы вероятностей токенов.
    /// </summary>
    public bool? LogProbs { get; set; }

    /// <summary>
    /// Количество топовых логитов для каждого шага (1-20).
    /// </summary>
    public int? TopLogprobs { get; set; }

    #endregion

    #region Настройки потоковой передачи

    public string StreamId { get; private set; }
    public string StreamMethod { get; private set; }

    /// <summary>
    /// Включена ли потоковая передача.
    /// </summary>
    public bool Stream => !string.IsNullOrEmpty(StreamId);

    #endregion

    #region Дополнительные настройки

    /// <summary>
    /// Настройки рассуждений (опционально).
    /// </summary>
    public ReasoningSettings ReasoningSettings { get; set; }

    /// <summary>
    /// Настройки усилий размышления для некоторых моделей, например GoogleAIStudio (Gemini, etc.).
    /// </summary>
    public string ReasoningEffort { get; set; }

    /// <summary>
    /// Формат ответа (Structured Output). Если задан — LLM гарантированно вернёт JSON по указанной схеме.
    /// Поддерживается OpenAI, Gemini, OpenRouter.
    /// </summary>
    public ResponseFormat ResponseFormat { get; set; }

    /// <summary>
    /// Модальности ответа: <c>["image", "text"]</c> для моделей, рисующих картинки.
    /// </summary>
    /// <remarks>
    /// Без этого поля модель с выводом изображений отвечает одним текстом: генерация картинки —
    /// это отдельная модальность ответа, а не отдельный эндпоинт. <c>null</c> — поле не уходит,
    /// поведение обычных текстовых моделей не меняется.
    /// </remarks>
    public List<string> Modalities { get; set; }

    /// <summary>
    /// Просить провайдера вернуть блок <c>usage</c> с фактической стоимостью запроса.
    /// </summary>
    /// <remarks>
    /// Токены агрегаторы отдают и так, а вот стоимость — только по запросу
    /// (<c>usage: {include: true}</c> у OpenRouter). Тому, кто считает по расходу деньги, цена
    /// апстрима нужнее пересчёта токенов по прайсу: она уже учитывает и скидки, и кэш.
    /// </remarks>
    public bool? IncludeUsage { get; set; }

    /// <summary>
    /// Просить провайдера присылать рассуждения модели (<c>include_reasoning</c>).
    /// </summary>
    /// <remarks>
    /// Отдельно от <see cref="ReasoningSettings"/>: те задают бюджет и усилие рассуждения, а это —
    /// возвращать ли его в ответе. Часть моделей молчит о рассуждениях, пока их не попросят явно.
    /// </remarks>
    public bool? IncludeReasoning { get; set; }

    #endregion

    #region Function Calling

    /// <summary>
    /// Список инструментов (функций), доступных модели.
    /// </summary>
    public List<ToolCalling.ToolDefinition> Tools { get; set; }

    /// <summary>
    /// Управление выбором инструмента: auto, none, required или конкретная функция.
    /// </summary>
    public ToolCalling.ToolChoice ToolChoice { get; set; }

    #endregion

    #region Конструктор

    public GenerateSettings(
        double temperature = 0.1,
        double? repetitionPenalty = 1.05,
        double? topP = 0.95,
        int? topK = 20,
        int? minTokens = 8,
        int? maxTokens = 3012,
        string streamId = null,
        string reasoningEffort = null,
        string streamMethod = "StreamMessage")
    {
        Temperature = temperature;
        RepetitionPenalty = repetitionPenalty;
        TopP = topP;
        TopK = topK;
        MinTokens = minTokens;
        MaxTokens = maxTokens;
        StreamId = streamId;
        StreamMethod = streamMethod;
        ReasoningEffort = reasoningEffort;
    }

    #endregion

    #region Копирование

    /// <summary>
    /// Копия настроек.
    /// </summary>
    /// <remarks>
    /// Один экземпляр настроек обычно задаётся при сборке и живёт дольше отдельного запроса,
    /// а запрос почти всегда что-то в них доопределяет: список инструментов, формат ответа,
    /// идентификатор потока. Правка общего экземпляра достаётся всем соседним запросам — при
    /// параллельной работе это чужой список инструментов в чужом запросе. Поэтому доопределять
    /// нужно копию, а не то, что дал вызывающий.
    /// <para>
    /// Копия поверхностная: <see cref="ReasoningSettings"/>, <see cref="ResponseFormat"/> и
    /// <see cref="ToolChoice"/> задаются целиком, а не правятся по месту, поэтому разделять их
    /// между копиями безопасно. Списки копируются — их как раз принято дополнять.
    /// </para>
    /// <para>
    /// Наследникам переопределять не нужно: копируется фактический тип со всеми его полями.
    /// </para>
    /// </remarks>
    public GenerateSettings Clone()
    {
        var copy = (GenerateSettings)MemberwiseClone();

        if (Tools != null)
            copy.Tools = [.. Tools];

        if (Modalities != null)
            copy.Modalities = [.. Modalities];

        return copy;
    }

    /// <summary>
    /// Копия настроек с заданным потоковым режимом.
    /// </summary>
    /// <param name="streamId">Идентификатор потока.</param>
    /// <param name="streamMethod">Метод потоковой выдачи; <c>null</c> — сохранить текущий.</param>
    /// <remarks>
    /// <see cref="StreamId"/> и <see cref="StreamMethod"/> задаются только при создании: включение
    /// потока меняет способ разбора ответа, и менять его у живого объекта, которым уже кто-то
    /// пользуется, нельзя. Здесь же создаётся новый объект, поэтому включение безопасно.
    /// </remarks>
    public GenerateSettings CloneWithStream(string streamId, string streamMethod = null)
    {
        var copy = Clone();
        copy.StreamId = streamId;
        copy.StreamMethod = streamMethod ?? StreamMethod;
        return copy;
    }

    #endregion
}
