using AI.LLM.Core.Models.Common.ToolCalling;

namespace AI.LLM.Core.Models.Common.Responses;

/// <summary>
/// Один кадр потокового ответа модели: то, что пришло в конкретном SSE-чанке.
/// </summary>
/// <remarks>
/// Отличие от <see cref="ChatCompletionsResponse"/> — в накоплении: там собранный ответ целиком,
/// здесь ровно та дельта, которую отдал провайдер. Поля независимы и почти всегда пусты: reasoning
/// приходит без content, финальный кадр несёт usage при пустом choices, изображения появляются
/// целым набором. Потребитель берёт то, что ему нужно, а собрать полный ответ из кадров умеет
/// <c>ChatLLMApi</c>.
/// </remarks>
[Serializable]
public class LLMStreamChunk
{
    /// <summary>Прирост видимого текста (<c>delta.content</c>).</summary>
    public string Content { get; set; }

    /// <summary>Прирост рассуждений (<c>delta.reasoning</c>, у части провайдеров <c>reasoning_content</c>).</summary>
    public string Reasoning { get; set; }

    /// <summary>Провайдер, исполнивший запрос (поле <c>provider</c> у OpenRouter).</summary>
    public string Provider { get; set; }

    /// <summary>Модель, названная самим ответом, — она может отличаться от запрошенной (роутинг агрегатора).</summary>
    public string Model { get; set; }

    /// <summary>Причина остановки генерации.</summary>
    public string FinishReason { get; set; }

    /// <summary>Причина остановки в терминах провайдера.</summary>
    public string NativeFinishReason { get; set; }

    /// <summary>
    /// Расход. Приходит один раз, в финальном кадре (<c>stream_options.include_usage</c>), обычно
    /// вместе с пустым <c>choices</c>.
    /// </summary>
    public Usage Usage { get; set; }

    /// <summary>
    /// Изображения кадра. <c>null</c> — в кадре их не было; пустой список — поле было, но пригодных
    /// картинок в нём нет. Различие существенно: провайдер присылает набор целиком, и непустой
    /// список заменяет предыдущий, а не дополняет его.
    /// </summary>
    public IReadOnlyList<ImageInfo> Images { get; set; }

    /// <summary>Дельты вызовов инструментов кадра; <c>null</c> — в кадре их не было.</summary>
    public IReadOnlyList<ToolCallDelta> ToolCalls { get; set; }

    /// <summary>Есть ли в кадре что показать пользователю.</summary>
    public bool HasText => !string.IsNullOrEmpty(Content) || !string.IsNullOrEmpty(Reasoning);
}

/// <summary>
/// Кусок вызова инструмента из потока: провайдер отдаёт их по частям и склеивает по
/// <see cref="Index"/>.
/// </summary>
/// <remarks>
/// Имя и аргументы приходят фрагментами (аргументы — почти всегда по нескольку символов), поэтому
/// склеиваются конкатенацией; <see cref="Id"/> и <see cref="Type"/> приходят целиком в первом кадре
/// вызова. <c>null</c> в любом поле означает «в этом кадре не было» — накопленное значение остаётся.
/// </remarks>
[Serializable]
public class ToolCallDelta
{
    /// <summary>Порядковый номер вызова в ответе — ключ склейки.</summary>
    public int Index { get; set; }

    /// <summary>Идентификатор вызова, на который потом ссылается сообщение с результатом.</summary>
    public string Id { get; set; }

    /// <summary>Тип вызова (<c>function</c>).</summary>
    public string Type { get; set; }

    /// <summary>Фрагмент имени функции.</summary>
    public string FunctionName { get; set; }

    /// <summary>Фрагмент JSON-аргументов.</summary>
    public string ArgumentsFragment { get; set; }

    /// <summary>Собирает <see cref="ToolCall"/> из накопленных дельт.</summary>
    /// <param name="builders">Накопитель по индексу вызова.</param>
    /// <param name="deltas">Дельты очередного кадра.</param>
    public static void Merge(IDictionary<int, ToolCall> builders, IEnumerable<ToolCallDelta> deltas)
    {
        if (builders == null || deltas == null) return;

        foreach (var delta in deltas)
        {
            if (!builders.TryGetValue(delta.Index, out var call))
            {
                call = new ToolCall { Function = new FunctionCall() };
                builders[delta.Index] = call;
            }

            if (delta.Id != null) call.Id = delta.Id;
            if (delta.Type != null) call.Type = delta.Type;
            if (delta.FunctionName != null) call.Function.Name = (call.Function.Name ?? "") + delta.FunctionName;
            if (delta.ArgumentsFragment != null)
                call.Function.Arguments = (call.Function.Arguments ?? "") + delta.ArgumentsFragment;
        }
    }
}
