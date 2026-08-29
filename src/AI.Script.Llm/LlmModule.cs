using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Llm;

/// <summary>
/// Пространство <c>llm</c>: обращения к языковой модели из скрипта.
/// </summary>
/// <remarks>
/// Модуль объектный, а не статический: клиент модели, эмбеддер и переранжировщик создаёт и
/// настраивает хост — со своими ключами, прокси и биллингом. Скрипт не может ни создать
/// клиента, ни подменить его, и это единственный способ сохранить смысл фразы «прогон стоит не
/// больше стольких-то токенов».
/// <para>
/// Каждая функция сначала спрашивает у прогона право обратиться к сети, а после ответа
/// сообщает расход. Порядок именно такой: право проверяется до запроса, расход считается
/// после, потому что до запроса никто не знает, сколько токенов вернёт модель.
/// </para>
/// </remarks>
[ScriptModule("llm", "Языковые модели: запросы, эмбеддинги; сеть может быть запрещена прогону")]
public sealed class LlmModule
{
    private readonly ILLMClient? _client;
    private readonly IEmbedderService? _embedder;
    private readonly IRerankerService<string, string>? _reranker;

    /// <summary>Создаёт модуль поверх служб, настроенных хостом.</summary>
    /// <param name="client">Клиент языковой модели; <c>null</c> — функции запросов недоступны.</param>
    /// <param name="embedder">Служба эмбеддингов; <c>null</c> — <c>llm.embed</c> недоступен.</param>
    /// <param name="reranker">Переранжировщик; <c>null</c> — <c>llm.rerank</c> недоступен.</param>
    public LlmModule(
        ILLMClient? client = null,
        IEmbedderService? embedder = null,
        IRerankerService<string, string>? reranker = null)
    {
        _client = client;
        _embedder = embedder;
        _reranker = reranker;
    }

    /// <summary>Собирает модуль языка из настроенных служб.</summary>
    public ScriptModule ToScriptModule() => ScriptModule.FromObject(this);

    // --- запросы ---

    [ScriptFn("ask", "Задаёт модели вопрос и возвращает ответ текстом",
        Example = "let answer = llm.ask(\"Перечисли три причины\", system: \"Отвечай списком\")")]
    public async Task<string> Ask(
        IScriptContext context,
        [ScriptParam("текст запроса")] string prompt,
        [ScriptParam("системная инструкция")] string system = "",
        [ScriptParam("температура: выше — разнообразнее")] double temperature = 0,
        [ScriptParam("потолок длины ответа в токенах")] int max_tokens = 0)
    {
        var messages = new List<LLMMessage>();

        if (!string.IsNullOrWhiteSpace(system)) messages.Add(LLMMessage.CreateMessage(Roles.System, system));

        messages.Add(LLMMessage.CreateMessage(Roles.User, prompt));

        return await SendAsync(context, messages, temperature, max_tokens, "llm.ask").ConfigureAwait(false);
    }

    /// <summary>
    /// Диалог: список записей вида <c>{ role: "user", text: "..." }</c>.
    /// </summary>
    /// <remarks>
    /// Роль — строка, а не отдельный тип языка: ролей три, и заводить ради них перечисление
    /// значило бы добавить в язык понятие, которое больше нигде не встречается.
    /// </remarks>
    [ScriptFn("chat", "Запрос с историей сообщений",
        Example = "llm.chat([{ role: \"system\", text: \"Ты аналитик\" }, { role: \"user\", text: \"Итог?\" }])")]
    public async Task<string> Chat(
        IScriptContext context,
        [ScriptParam("список записей { role, text }")] ScriptList messages,
        [ScriptParam("температура")] double temperature = 0,
        [ScriptParam("потолок длины ответа в токенах")] int max_tokens = 0)
    {
        var history = new List<LLMMessage>(messages.Count);

        for (int i = 0; i < messages.Count; i++)
        {
            if (messages[i].Type != ScriptType.Record)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"llm.chat: элемент {i} — {messages[i].Type.ToName()}, а нужна запись",
                    "каждое сообщение записывается как { role: \"user\", text: \"...\" }");
            }

            ScriptRecord message = messages[i].AsRecord();

            history.Add(new LLMMessage(RoleOf(message, i), TextOf(message, i)));
        }

        if (history.Count == 0)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                "llm.chat: список сообщений пуст");
        }

        return await SendAsync(context, history, temperature, max_tokens, "llm.chat").ConfigureAwait(false);
    }

    /// <summary>
    /// Ответ модели разбором в запись.
    /// </summary>
    /// <remarks>
    /// Модели свойственно обрамлять JSON пояснениями и оградами ```json — поэтому разбирается
    /// не весь ответ, а первый сбалансированный объект в нём. Просить «ответь только JSON» и
    /// надеяться — значит получать отказ на каждом десятом вызове.
    /// </remarks>
    [ScriptFn("json", "Запрос со структурированным ответом: разбирает JSON из ответа модели",
        Example = "llm.json(\"Разбери отзыв\", shape: { тон: \"строка\", оценка: \"число\" })")]
    public async Task<ScriptValue> Json(
        IScriptContext context,
        [ScriptParam("текст запроса")] string prompt,
        [ScriptParam("образец ответа: поля и что в них класть")] ScriptValue shape = default,
        [ScriptParam("системная инструкция")] string system = "",
        [ScriptParam("температура")] double temperature = 0)
    {
        string instruction = shape.Type == ScriptType.Record
            ? $"{prompt}\n\nОтветь одним объектом JSON такого вида:\n{ScriptFormatter.Format(shape, quoteStrings: true)}"
            : $"{prompt}\n\nОтветь одним объектом JSON без пояснений.";

        string answer = await Ask(context, instruction, system, temperature).ConfigureAwait(false);

        if (JsonIsland.TryExtract(answer, out ScriptValue value)) return value;

        throw new ScriptError(
            DiagnosticCodes.BadFileFormat,
            "llm.json: в ответе модели нет объекта JSON",
            $"модель ответила: {Shorten(answer)}");
    }

    /// <summary>
    /// Относит текст к одной из заданных меток.
    /// </summary>
    /// <remarks>
    /// Ответ приводится к ближайшей из меток, а не принимается как есть: модель охотно
    /// отвечает «Скорее положительный» там, где просили «положительный», и сырой ответ ломал бы
    /// сравнение в следующей же строке скрипта.
    /// </remarks>
    [ScriptFn("classify", "Относит текст к одной из меток",
        Example = "llm.classify(отзыв, labels: [\"положительный\", \"отрицательный\"])")]
    public async Task<string> Classify(
        IScriptContext context,
        [ScriptParam("текст")] string text,
        [ScriptParam("список меток")] ScriptList labels,
        [ScriptParam("пояснение к задаче")] string instruction = "")
    {
        if (labels.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "llm.classify: список меток пуст");

        var names = new List<string>(labels.Count);

        for (int i = 0; i < labels.Count; i++) names.Add(ScriptFormatter.Format(labels[i]));

        string task = string.IsNullOrWhiteSpace(instruction)
            ? "Отнеси текст к одной из меток."
            : instruction;

        string answer = await Ask(
            context,
            $"{task}\n\nМетки: {string.Join(", ", names)}\n\nТекст:\n{text}\n\nОтветь одной меткой без пояснений.",
            system: "Ты классификатор. В ответе только метка из списка.").ConfigureAwait(false);

        return Nearest(answer, names);
    }

    // --- векторные представления ---

    [ScriptFn("embed", "Векторное представление текста", Example = "let v = llm.embed(\"текст запроса\")")]
    public async Task<Vector> Embed(
        IScriptContext context,
        [ScriptParam("текст")] string text)
    {
        IEmbedderService embedder = RequireEmbedder("llm.embed");

        context.Network.Require("llm.embed");

        context.BeginExternalCall();

        Vector vector = await embedder.EncodeAsync(text, context.Cancellation).ConfigureAwait(false);

        context.CountExternal();
        context.CountAllocation(vector.Count);

        return vector;
    }

    /// <summary>
    /// Векторы для списка текстов: строка матрицы — один текст.
    /// </summary>
    /// <remarks>
    /// Отдельная функция, а не <c>core.map</c> с <c>llm.embed</c>: эмбеддеры принимают пачку
    /// за один запрос, и поштучный вызов стоит в разы дороже при том же результате.
    /// </remarks>
    [ScriptFn("embed_all", "Векторные представления списка текстов одной пачкой",
        Example = "let m = llm.embed_all(документы)")]
    public async Task<Matrix> EmbedAll(
        IScriptContext context,
        [ScriptParam("список текстов")] ScriptList texts)
    {
        IEmbedderService embedder = RequireEmbedder("llm.embed_all");

        context.Network.Require("llm.embed_all");

        context.BeginExternalCall();

        if (texts.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "llm.embed_all: список текстов пуст");

        var input = new List<string>(texts.Count);

        for (int i = 0; i < texts.Count; i++) input.Add(ScriptFormatter.Format(texts[i]));

        Vector[] vectors = await embedder.EncodeAsync(input, context.Cancellation).ConfigureAwait(false);

        context.CountExternal();

        return Embeddings.ToMatrix(vectors, context);
    }

    [ScriptFn("rerank", "Оценивает близость запроса к каждому документу",
        Example = "let scores = llm.rerank(question, docs)")]
    public async Task<Vector> Rerank(
        IScriptContext context,
        [ScriptParam("запрос")] string query,
        [ScriptParam("список документов")] ScriptList documents)
    {
        if (_reranker == null)
        {
            throw new ScriptError(
                DiagnosticCodes.UnknownFunction,
                "llm.rerank: переранжировщик не подключён хостом",
                "оцените близость эмбеддингами: llm.embed_all и search.of");
        }

        context.Network.Require("llm.rerank");

        context.BeginExternalCall();

        var input = new List<string>(documents.Count);

        for (int i = 0; i < documents.Count; i++) input.Add(ScriptFormatter.Format(documents[i]));

        Vector scores = await _reranker.SimsAsync(query, input).ConfigureAwait(false);

        context.CountExternal();

        return scores;
    }

    // --- учёт ---

    /// <summary>
    /// Сколько прогон уже потратил.
    /// </summary>
    /// <remarks>
    /// Доступно скрипту, а не только хосту: конвейер, который сам решает, стоит ли делать ещё
    /// один проход по модели, должен видеть остаток, иначе решение принимает потолок — отказом
    /// посреди работы.
    /// </remarks>
    [ScriptFn("usage", "Расход прогона: вызовы, токены, стоимость", Example = "emit spent = llm.usage()")]
    public static ScriptRecord Usage(IScriptContext context)
    {
        ExternalUsage usage = context.Usage;

        return ScriptRecord.From(
        [
            new("calls", ScriptValue.Num(usage.Calls)),
            new("tokens", ScriptValue.Num(usage.Tokens)),
            new("cost", ScriptValue.Num((double)usage.Cost)),
        ]);
    }

    /// <summary>
    /// Доступна ли модель этому прогону.
    /// </summary>
    /// <remarks>
    /// Нужна скрипту, который умеет и с моделью, и без неё: спросить заранее дешевле, чем
    /// ловить отказ в <c>try</c> и разбирать по тексту, чего именно не хватило.
    /// </remarks>
    [ScriptFn("available", "Подключена ли модель и разрешена ли сеть",
        Example = "let answer = if llm.available() { llm.ask(question) } else { \"модель недоступна\" }")]
    public bool Available(IScriptContext context) => _client != null && context.Network.Enabled;

    // --- внутреннее ---

    private async Task<string> SendAsync(
        IScriptContext context,
        IReadOnlyList<LLMMessage> messages,
        double temperature,
        int maxTokens,
        string what)
    {
        ILLMClient client = _client ?? throw new ScriptError(
            DiagnosticCodes.UnknownFunction,
            $"{what}: языковая модель не подключена хостом",
            "хост подключает модуль llm со своим клиентом; без него запросы недоступны");

        context.Network.Require(what);

        context.BeginExternalCall();

        var settings = new GenerateSettings();

        if (temperature > 0) settings.Temperature = temperature;
        if (maxTokens > 0) settings.MaxTokens = maxTokens;

        ChatCompletionsResponse response = await client
            .SendFullAsync(messages, settings, context.Cancellation)
            .ConfigureAwait(false);

        // Расход считается всегда, даже если ответ пуст: запрос уже оплачен.
        context.CountExternal(TokensOf(response), CostOf(response));

        string? answer = TextOf(response);

        if (answer != null) return answer;

        throw new ScriptError(
            DiagnosticCodes.FunctionFailed,
            $"{what}: модель вернула пустой ответ",
            "проверьте потолок длины ответа и настройки провайдера");
    }

    private static long TokensOf(ChatCompletionsResponse response)
    {
        Usage? usage = response.Usage;

        if (usage == null) return 0;

        return usage.TotalTokens > 0 ? usage.TotalTokens : usage.PromptTokens + usage.CompletionTokens;
    }

    /// <summary>
    /// Стоимость вызова из ответа провайдера.
    /// </summary>
    /// <remarks>
    /// Числовые значения читаются напрямую, и лишь остальное отдаётся <c>CostExtractor</c>.
    /// Тот приводит значение к строке текущей культурой, а разбирает инвариантной: под русской
    /// локалью <c>0,25</c> превращается в <c>25</c>, потому что запятая читается как разделитель
    /// тысяч. Ошибка в сто раз в учёте расходов — не то, с чем стоит мириться ради одной строки.
    /// </remarks>
    private static decimal CostOf(ChatCompletionsResponse response)
    {
        object? cost = response.Usage?.Cost;

        return cost switch
        {
            null => 0,
            decimal value => value,
            double value => (decimal)value,
            float value => (decimal)value,
            int value => value,
            long value => value,
            _ => CostExtractor.TryExtract(cost) ?? 0,
        };
    }

    private static string? TextOf(ChatCompletionsResponse response)
    {
        if (response.Choices.Count == 0) return null;

        object? content = response.Choices[0].Message?.Content;

        return content?.ToString();
    }

    private IEmbedderService RequireEmbedder(string what) =>
        _embedder ?? throw new ScriptError(
            DiagnosticCodes.UnknownFunction,
            $"{what}: служба эмбеддингов не подключена хостом",
            "подключите модуль llm с эмбеддером либо считайте близость по словам: nlp.similarity");

    private static string RoleOf(ScriptRecord message, int index)
    {
        if (!message.TryGet("role", out ScriptValue role) || role.Type != ScriptType.Str)
        {
            throw new ScriptError(
                DiagnosticCodes.MissingArgument,
                $"llm.chat: у сообщения {index} нет поля 'role'",
                "роль — одна из строк: \"system\", \"user\", \"assistant\"");
        }

        string name = role.AsString("role");

        return name is LLMMessage.SystemRole or LLMMessage.UserRole or LLMMessage.AssistantRole
            ? name
            : throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"llm.chat: неизвестная роль '{name}' в сообщении {index}",
                "известны: \"system\", \"user\", \"assistant\"");
    }

    private static string TextOf(ScriptRecord message, int index)
    {
        if (message.TryGet("text", out ScriptValue text)) return ScriptFormatter.Format(text);

        throw new ScriptError(
            DiagnosticCodes.MissingArgument,
            $"llm.chat: у сообщения {index} нет поля 'text'");
    }

    /// <summary>Ближайшая метка к ответу модели: точное совпадение, потом вхождение.</summary>
    private static string Nearest(string answer, IReadOnlyList<string> labels)
    {
        string trimmed = answer.Trim().Trim('.', '"', '\'', '«', '»');

        foreach (string label in labels)
        {
            if (string.Equals(label, trimmed, StringComparison.OrdinalIgnoreCase)) return label;
        }

        foreach (string label in labels)
        {
            if (trimmed.Contains(label, StringComparison.OrdinalIgnoreCase)) return label;
        }

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"llm.classify: ответ модели не соответствует ни одной метке — {Shorten(answer)}",
            $"метки: {string.Join(", ", labels)}");
    }

    private static string Shorten(string text) =>
        text.Length <= 200 ? text : text[..200] + "…";
}
