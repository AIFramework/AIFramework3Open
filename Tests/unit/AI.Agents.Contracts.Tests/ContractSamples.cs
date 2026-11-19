using System.Text.Json;
using System.Text.Json.Nodes;
using AI.Agents.Contracts.Citations;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Образцы для круга через JSON: по одному или несколько на каждый публичный неабстрактный тип контрактов.
/// </summary>
/// <remarks>
/// Первый образец типа заполняет каждое свойство с init (не null, не пустая коллекция, не false, не ноль) —
/// иначе свойство, которое теряется при чтении, прошло бы незамеченным. Следующие образцы проверяют
/// частные случаи и могут быть заполнены не полностью. Перечисления сюда не входят: тест перебирает
/// все их значения сам. Новый тип без образца роняет тест <see cref="JsonRoundTripTests"/>.
/// </remarks>
internal static class ContractSamples
{
    /// <summary>
    /// Типы, которые несут порты хоста (живые объекты) и поэтому в JSON не пишутся. Тест проверяет, что у
    /// каждого такого типа действительно есть свойство-интерфейс контрактов; данные этих типов вынесены в
    /// отдельные записи и проходят круг сами.
    /// </summary>
    public static readonly IReadOnlyDictionary<Type, string> RuntimeOnly = new Dictionary<Type, string>
    {
        [typeof(EngineInput)] =
            "порты IToolSet, IModelPort, IRunBudget, IStepRunner; данные входа — EngineTask, EngineContext, State, Options",
    };

    /// <summary>
    /// Взаимоисключающие свойства: заполнить их все в одном образце нельзя по правилу типа. Первый образец
    /// заполняет одно из группы, остальные свойства группы должны быть заполнены в следующих образцах.
    /// </summary>
    public static readonly IReadOnlyDictionary<Type, string[]> Exclusive = new Dictionary<Type, string[]>
    {
        [typeof(AgentInstructions)] = [nameof(AgentInstructions.Key), nameof(AgentInstructions.Text)],
        [typeof(MessagePart.Audio)] = [nameof(MessagePart.Audio.Data), nameof(MessagePart.Audio.Uri)],
    };

    /// <summary>Образцы по типам.</summary>
    public static IReadOnlyDictionary<Type, Func<object>[]> All { get; } = new Dictionary<Type, Func<object>[]>
    {
        // Tools
        [typeof(About)] =
        [
            () => FullAbout(),
            () => About.FromText("Одна строка описания"),
        ],
        [typeof(ToolSpec)] = [() => FullSpec()],
        [typeof(ToolCall)] =
        [
            () => FullCall(),
            () => new ToolCall { Id = "call_2", Name = "search", Arguments = Json("\"не JSON: строка как есть\"") },
        ],
        [typeof(ToolResult)] = [() => FullResult()],
        [typeof(ToolUpdate.Progress)] = [() => FullProgress()],
        [typeof(ToolUpdate.Final)] = [() => new ToolUpdate.Final { Result = FullResult() }],
        [typeof(ToolContext)] = [() => new ToolContext { RunId = "run-1", Path = FullPath() }],
        [typeof(ToolFile)] = [() => FullFile()],
        [typeof(Origin)] =
        [
            () => new Origin { Kind = OriginKind.External, Address = "https://example.org/page" },
            () => new Origin { Kind = OriginKind.Trusted },
        ],

        // Citations
        [typeof(Citation)] = [() => FullCitation()],
        [typeof(SourceLocation)] =
        [
            () => FullLocation(),
            () => new SourceLocation { Path = [FullSegment()], Page = 1 },
            () => new SourceLocation { Path = [], Lines = new LineRange { Start = 5, End = 5 } },
            () => new SourceLocation { Time = new TimeInterval { Start = TimeSpan.Zero, End = TimeSpan.Zero } },
        ],
        [typeof(LocationSegment)] =
        [
            () => FullSegment(),
            () => new LocationSegment { Kind = LocationSegment.Kinds.Slide, Index = 3 },
            () => new LocationSegment { Kind = LocationSegment.Kinds.Sheet, Id = "Итоги" },
        ],
        [typeof(LineRange)] = [() => new LineRange { Start = 10, End = 12 }],
        [typeof(TimeInterval)] =
        [
            () => new TimeInterval { Start = TimeSpan.FromSeconds(83.5), End = TimeSpan.FromMinutes(2) },
        ],
        [typeof(RelativeRect)] =
        [
            () => FullRect(),
            () => new RelativeRect { X = 0, Y = 0, Width = 1, Height = 1 },
        ],

        // Engines
        [typeof(StepPath)] = [() => FullPath(), () => new StepPath { Tier = 1 }],
        [typeof(StopCode)] = [() => StopCode.StepLimit, () => new StopCode("my_reason")],
        [typeof(AgentRef)] =
        [
            () => new AgentRef { Id = "writer", Version = 3 },
            () => new AgentRef { Id = "writer" },
        ],
        [typeof(AgentDefinition)] = [() => FullAgent(), () => MinimalAgent()],
        [typeof(AgentInstructions)] =
        [
            () => AgentInstructions.FromKey("prompts/contract-analyst"),
            () => AgentInstructions.FromText("Отвечай по договору, цитируй пункты"),
        ],
        [typeof(EngineChoice)] =
        [
            () => new EngineChoice { Key = "react", Options = Obj("""{"maxThoughts":3}""") },
            () => new EngineChoice { Key = "direct" },
        ],
        [typeof(AgentLimits)] =
        [
            () => FullLimits(),
            () => new AgentLimits { Steps = 5 },
        ],
        [typeof(InputField)] = [() => FullField()],
        [typeof(Question)] = [() => FullQuestion()],
        [typeof(Answer)] = [() => FullAnswer()],
        [typeof(EngineTask)] = [() => FullTask()],
        [typeof(EngineContext)] = [() => FullContext()],
        [typeof(EngineResult)] = [() => FullEngineResult()],
        [typeof(EngineEvent.Step)] = [() => new EngineEvent.Step { Path = FullPath(), Thought = "Нужно найти курс" }],
        [typeof(EngineEvent.ToolStarted)] = [() => new EngineEvent.ToolStarted { Path = FullPath(), Call = FullCall() }],
        [typeof(EngineEvent.ToolProgress)] =
        [
            () => new EngineEvent.ToolProgress { Path = FullPath(), Call = FullCall(), Update = FullProgress() },
        ],
        [typeof(EngineEvent.ToolFinished)] =
        [
            () => new EngineEvent.ToolFinished { Path = FullPath(), Call = FullCall(), Result = FullResult() },
        ],
        [typeof(EngineEvent.Text)] = [() => new EngineEvent.Text { Chunk = "Курс на сегодня" }],
        [typeof(EngineEvent.Reasoning)] = [() => new EngineEvent.Reasoning { Chunk = "Сначала поиск" }],
        [typeof(EngineEvent.Spent)] = [() => new EngineEvent.Spent { Usage = FullUsage() }],
        [typeof(EngineEvent.Saved)] = [() => new EngineEvent.Saved { Path = FullPath(), State = Obj("""{"step":2}""") }],
        [typeof(EngineEvent.Finished)] = [() => new EngineEvent.Finished { Result = FullEngineResult() }],

        // Models
        [typeof(ModelRole)] = [() => ModelRole.Decide, () => new ModelRole("extract")],
        [typeof(ModelCapabilities)] =
        [
            () => new ModelCapabilities
            {
                ToolCalling = true, Images = true, Audio = true, StructuredOutput = true, Streaming = true,
                ContextTokens = 128000,
            },
        ],
        [typeof(MessagePart.Text)] = [() => FullTextPart()],
        [typeof(MessagePart.Image)] = [() => FullImagePart()],
        [typeof(MessagePart.Audio)] = [() => AudioWithData(), () => AudioWithUri()],
        [typeof(ModelMessage)] = [() => FullMessage()],
        [typeof(ModelAsk)] = [() => FullAsk()],
        [typeof(ModelUsage)] = [() => FullUsage()],
        [typeof(LlmAnswer)] =
        [
            () => FullAnswerOfModel(),
            () => new LlmAnswer { Status = LlmStatus.Failed, Failure = LlmFailure.ContextOverflow },
        ],
        [typeof(LlmChunk.Text)] = [() => new LlmChunk.Text { Chunk = "Курс " }],
        [typeof(LlmChunk.Reasoning)] = [() => new LlmChunk.Reasoning { Chunk = "Сначала поиск" }],
        [typeof(LlmChunk.Final)] = [() => new LlmChunk.Final { Answer = FullAnswerOfModel() }],
    };

    /// <summary>JSON-элемент, не зависящий от документа.</summary>
    public static JsonElement Json(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>Объект JSON.</summary>
    public static JsonObject Obj(string text) => JsonNode.Parse(text)!.AsObject();

    private static About FullAbout() => new()
    {
        Does = "Ищет курс валют на дату",
        DoesNot = "Не конвертирует суммы",
        Rules = "Дата в формате ISO 8601",
        Examples = ["курс евро на 2026-11-19"],
        Guardrails = "Курс справочный",
    };

    private static ToolSpec FullSpec() => new()
    {
        Name = "get_rate",
        Description = "Курс валюты на дату",
        Parameters = Json("""{"type":"object","properties":{"code":{"type":"string"}},"required":["code"]}"""),
        Action = ToolAction.Read,
        Group = "finance",
        About = FullAbout(),
    };

    private static ToolCall FullCall() => new()
    {
        Id = "call_1",
        Name = "get_rate",
        Arguments = Json("""{"code":"EUR"}"""),
        EffectKey = "get_rate:{\"code\":\"EUR\"}",
    };

    private static ToolFile FullFile() => new()
    {
        ContentType = "image/png",
        Name = "screen.png",
        Uri = "file:///tmp/screen.png",
        Content = [0x89, 0x50, 0x4E, 0x47],
        ShowToModel = true,
    };

    private static Citation FullCitation() => new()
    {
        Kind = CitationKind.Web,
        Title = "Курсы валют",
        Quote = "EUR 101,5",
        Url = "https://example.org/copy",
        Claim = "Евро стоит 101,5",
        Verified = true,
        Prefix = "курс ",
        Suffix = " руб.",
        Location = FullLocation(),
        At = 42,
        Start = 10,
        End = 30,
        Site = "https://example.org/rates",
        Step = 2,
        Taken = new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(3)),
        Basis = [new Citation { Kind = CitationKind.Calculation, Title = "Расчет", Quote = "a + b" }],
    };

    /// <summary>Место из двух сегментов пути со всеми частями.</summary>
    public static SourceLocation FullLocation() => new()
    {
        Path =
        [
            new LocationSegment { Kind = LocationSegment.Kinds.Slide, Index = 3 },
            FullSegment(),
        ],
        Page = 2,
        Lines = new LineRange { Start = 10, End = 12 },
        Time = new TimeInterval { Start = TimeSpan.FromSeconds(5), End = TimeSpan.FromSeconds(9.25) },
        Rect = FullRect(),
    };

    private static LocationSegment FullSegment() => new()
    {
        Kind = LocationSegment.Kinds.Image,
        Index = 2,
        Id = "shape-17",
    };

    private static RelativeRect FullRect() => new() { X = 0.1, Y = 0.25, Width = 0.9, Height = 0.5 };

    private static AgentLimits FullLimits() => new()
    {
        Steps = 20,
        Cost = 1.5m,
        Duration = TimeSpan.FromMinutes(10),
    };

    /// <summary>Запись агента со всеми полями.</summary>
    public static AgentDefinition FullAgent() => new()
    {
        Id = "contract-analyst",
        Version = 4,
        Name = "Аналитик договоров",
        Role = "Разбирает договоры поставки и находит риски",
        About = FullAbout(),
        Tools = ["get_rate", "finance"],
        Engine = new EngineChoice { Key = "react", Options = Obj("""{"maxThoughts":3}""") },
        Model = ModelRole.Judge,
        Limits = FullLimits(),
        Instructions = AgentInstructions.FromKey("prompts/contract-analyst"),
        Inputs = [FullField()],
        Agents = [new AgentRef { Id = "writer", Version = 3 }],
        Output = ["text", "table"],
        Acceptance = "contract-checks",
    };

    /// <summary>Запись агента только с обязательными полями.</summary>
    public static AgentDefinition MinimalAgent() => new()
    {
        Id = "echo",
        Version = 1,
        Name = "Эхо",
        Role = "Повторяет задачу своими словами",
        Instructions = AgentInstructions.FromText("Перескажи задачу"),
    };

    private static InputField FullField() => new()
    {
        Key = "currency",
        Text = "Какая валюта?",
        Choices = ["EUR", "USD"],
    };

    private static Question FullQuestion() => new()
    {
        Id = "q-1",
        Kind = QuestionKind.Confirm,
        Text = "Отправить письмо?",
        AskedBy = "run-child-7",
        Fields = [FullField()],
        EffectKey = "send_mail:abc",
        Action = ToolAction.Irreversible,
        Target = "boss@example.org",
    };

    private static Answer FullAnswer() => new()
    {
        QuestionId = "q-1",
        Values = new Dictionary<string, JsonElement>
        {
            ["currency"] = Json("\"EUR\""),
            ["amount"] = Json("100.5"),
            ["sites"] = Json("""["a","b"]"""),
            ["range"] = Json("""{"from":1,"to":2}"""),
        },
        Confirmed = true,
    };

    private static ToolResult FullResult() => new()
    {
        Status = ToolStatus.AwaitsUser,
        Text = "Нужно согласие",
        Origin = new Origin { Kind = OriginKind.External, Address = "https://example.org" },
        ErrorCode = "needs_confirm",
        Data = Json("""{"rate":101.5}"""),
        Files = [FullFile()],
        Evidence = [FullCitation()],
        Question = FullQuestion(),
        Raised = ToolAction.Irreversible,
        EndsRun = true,
    };

    private static ToolUpdate.Progress FullProgress() => new()
    {
        Message = "Открываю страницу",
        Data = Json("""{"percent":40}"""),
    };

    private static StepPath FullPath() => new() { Tier = 3, Branch = "b2", Child = "run-child-7" };

    private static EngineTask FullTask() => new()
    {
        Text = "Найди курс евро",
        Deliverable = "Одно число",
        Remarks = ["Нет источника"],
        Answer = FullAnswer(),
    };

    private static ModelUsage FullUsage() => new() { InputTokens = 1200, OutputTokens = 300, Cost = 0.0042m };

    private static MessagePart.Text FullTextPart() => new()
    {
        Origin = new Origin { Kind = OriginKind.Trusted, Address = "host" },
        Content = "Привет",
    };

    private static MessagePart.Image FullImagePart() => new()
    {
        Origin = new Origin { Kind = OriginKind.External, Address = "https://example.org/a.png" },
        ContentType = "image/png",
        Data = [1, 2, 3],
        Uri = "https://example.org/a.png",
    };

    /// <summary>Звук байтами.</summary>
    public static MessagePart.Audio AudioWithData() => new()
    {
        Origin = new Origin { Kind = OriginKind.Trusted, Address = "microphone" },
        ContentType = "audio/wav",
        Data = [0x52, 0x49, 0x46, 0x46],
    };

    /// <summary>Звук по адресу.</summary>
    public static MessagePart.Audio AudioWithUri() => new()
    {
        Origin = new Origin { Kind = OriginKind.External, Address = "https://example.org/call.mp3" },
        ContentType = "audio/mpeg",
        Uri = "https://example.org/call.mp3",
    };

    private static ModelMessage FullMessage() => new()
    {
        Role = MessageRole.Assistant,
        Parts = [FullTextPart(), FullImagePart(), AudioWithData()],
        ToolCalls = [FullCall()],
        ToolCallId = "call_0",
        Cache = true,
    };

    private static EngineContext FullContext() => new()
    {
        Instructions = "Отвечай кратко",
        History = [FullMessage()],
        Files = [FullFile()],
    };

    private static EngineResult FullEngineResult() => new()
    {
        Status = EngineStatus.Incomplete,
        StopCode = StopCode.StepLimit,
        Text = "Частичный ответ",
        Payload = Json("""{"rows":3}"""),
        Evidence = [FullCitation()],
        Files = [FullFile()],
        Unfinished = ["Сверить второй источник"],
        Question = FullQuestion(),
        Usage = FullUsage(),
        State = Obj("""{"trace":[1,2]}"""),
    };

    private static ModelAsk FullAsk() => new()
    {
        Role = ModelRole.Decide,
        Messages = [FullMessage()],
        ToolMode = ToolMode.Named,
        Tools = [FullSpec()],
        ToolName = "get_rate",
        Schema = Json("""{"type":"object"}"""),
        MaxTokens = 512,
    };

    private static LlmAnswer FullAnswerOfModel() => new()
    {
        Status = LlmStatus.Failed,
        Text = "Частично",
        Reasoning = "Думаю",
        ToolCalls = [FullCall()],
        Failure = LlmFailure.RateLimit,
        Model = "provider/model-1",
        Usage = FullUsage(),
    };
}
