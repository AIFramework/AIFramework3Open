using AI.Script.Hosting;
using AI.Script.Llm;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Std;

namespace AI.Script.UnitTests;

/// <summary>
/// Пространства <c>llm</c> и <c>search</c>: политика сети, учёт расходов, разбор ответов.
/// </summary>
public sealed class LlmTests
{
    private static ScriptHost Host(FakeLlm? llm = null, FakeEmbedder? embedder = null) =>
        StandardLibrary.CreateHost().UseLlm(llm, embedder);

    private static RunOptions Online(int calls = 0, long tokens = 0, decimal cost = 0)
    {
        var options = new RunOptions { Network = NetworkPolicy.Allowed };

        options.Limits.ExternalCalls = calls;
        options.Limits.ExternalTokens = tokens;
        options.Limits.ExternalCost = cost;

        return options;
    }

    // --- политика сети ---

    /// <summary>
    /// Подключённый модуль — это «возможность есть», а не «этому прогону можно»: без явного
    /// разрешения обращение к модели отклоняется.
    /// </summary>
    [Fact]
    public void Network_DeniedByDefault()
    {
        RunResult result = Script.RunWith(Host(new FakeLlm("ответ")), "emit r = llm.ask(\"привет\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.NetworkDenied, result.Error!.Code);
    }

    [Fact]
    public void Network_WhitelistRejectsOtherHosts()
    {
        var policy = NetworkPolicy.AllowHosts("openrouter.ai");

        policy.Require("проверка", "openrouter.ai");

        ScriptError error = Assert.Throws<ScriptError>(() => policy.Require("проверка", "example.com"));

        Assert.Equal(DiagnosticCodes.NetworkDenied, error.Code);
    }

    [Fact]
    public void Available_IsFalseWithoutNetwork()
    {
        Assert.Equal(false, Script.RunWith(Host(new FakeLlm()), "emit r = llm.available()").Emitted["r"]);

        Assert.Equal(true, Script.RunWith(Host(new FakeLlm()), "emit r = llm.available()", Online()).Emitted["r"]);
    }

    [Fact]
    public void Available_IsFalseWithoutClient()
    {
        Assert.Equal(false, Script.RunWith(Host(), "emit r = llm.available()", Online()).Emitted["r"]);
    }

    [Fact]
    public void Ask_WithoutClient_SaysSo()
    {
        RunResult result = Script.RunWith(Host(), "emit r = llm.ask(\"привет\")", Online());

        Assert.False(result.Success);
        Assert.Contains("не подключена хостом", result.Error!.Message, StringComparison.Ordinal);
    }

    // --- запросы и учёт ---

    [Fact]
    public void Ask_ReturnsAnswerAndCountsUsage()
    {
        var llm = new FakeLlm("сорок два") { Tokens = 130, Cost = 0.25m };

        RunResult result = Script.RunWith(Host(llm), """
            let ответ = llm.ask("сколько?", system: "коротко")
            let расход = llm.usage()

            emit ответ = ответ
            emit вызовов = расход.calls
            emit токенов = расход.tokens
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal("сорок два", result.Emitted["ответ"]);
        Assert.Equal(1.0, result.Emitted["вызовов"]);
        Assert.Equal(130.0, result.Emitted["токенов"]);

        Assert.Equal(1, result.Stats.ExternalCalls);
        Assert.Equal(130, result.Stats.ExternalTokens);
        Assert.Equal(0.25m, result.Stats.ExternalCost);

        // Системная инструкция уходит отдельным сообщением, а не приклеивается к запросу.
        Assert.Equal(2, llm.LastMessages.Count);
        Assert.Equal(LLMMessageRoles.System, llm.LastMessages[0].Role);
    }

    [Fact]
    public void Chat_PassesHistoryInOrder()
    {
        var llm = new FakeLlm("итог");

        RunResult result = Script.RunWith(Host(llm), """
            emit r = llm.chat([
                { role: "system", text: "Ты аналитик" },
                { role: "user", text: "Первый" },
                { role: "assistant", text: "Понял" },
                { role: "user", text: "Второй" }
            ])
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(4, llm.LastMessages.Count);
        Assert.Equal("Второй", llm.LastMessages[3].Content?.ToString());
    }

    [Fact]
    public void Chat_UnknownRole_IsReported()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = llm.chat([{ role: \"эксперт\", text: \"да\" }])",
            Online(),
            Host(new FakeLlm("ok")));

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("эксперт", error.Message, StringComparison.Ordinal);
    }

    // --- потолки расходов ---

    [Fact]
    public void CostLimit_StopsRun()
    {
        var llm = new FakeLlm("раз", "два", "три") { Tokens = 10 };

        RunResult result = Script.RunWith(Host(llm), """
            emit a = llm.ask("раз")
            emit b = llm.ask("два")
            emit c = llm.ask("три")
            """, Online(calls: 2));

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.CostLimit, result.Error!.Code);

        // Ровно два вызова: потолок обязан запретить третий, а не сообщить о нём после оплаты.
        Assert.Equal(2, result.Stats.ExternalCalls);
        Assert.Equal(2, llm.Requests);
    }

    [Fact]
    public void TokenLimit_StopsRun()
    {
        var llm = new FakeLlm("раз", "два") { Tokens = 600 };

        RunResult result = Script.RunWith(Host(llm), "emit a = llm.ask(\"раз\")\nemit b = llm.ask(\"два\")",
            Online(tokens: 1000));

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.CostLimit, result.Error!.Code);
    }

    /// <summary>Потолок расходов — прерывание прогона: скрипт не может его поймать и продолжить.</summary>
    [Fact]
    public void CostLimit_IsNotCatchable()
    {
        var llm = new FakeLlm("раз", "два") { Tokens = 10 };

        RunResult result = Script.RunWith(Host(llm), """
            emit a = llm.ask("раз")

            try {
                emit b = llm.ask("два")
            } catch e {
                emit поймано = true
            }
            """, Online(calls: 1));

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.CostLimit, result.Error!.Code);
        Assert.DoesNotContain("поймано", result.Emitted.Keys);
    }

    // --- разбор ответов ---

    /// <summary>
    /// Модель обрамляет JSON пояснениями и оградами, даже когда просили этого не делать.
    /// Разбирается первый сбалансированный объект, а не весь ответ.
    /// </summary>
    [Fact]
    public void Json_ExtractsObjectFromVerboseAnswer()
    {
        var llm = new FakeLlm("""
            Конечно! Вот разбор отзыва:

            ```json
            { "тон": "положительный", "оценка": 5 }
            ```

            Если нужно что-то ещё — скажите.
            """);

        RunResult result = Script.RunWith(Host(llm), """
            let разбор = llm.json("Разбери отзыв", shape: { тон: "строка", оценка: "число" })

            emit тон = разбор.тон
            emit оценка = разбор.оценка
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal("положительный", result.Emitted["тон"]);
        Assert.Equal(5.0, result.Emitted["оценка"]);
    }

    [Fact]
    public void Json_BracesInsideStrings_DoNotBreakParsing()
    {
        var llm = new FakeLlm("{ \"текст\": \"смотри {пример} внутри\", \"n\": 1 }");

        RunResult result = Script.RunWith(Host(llm), "emit r = llm.json(\"дай\").текст", Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal("смотри {пример} внутри", result.Emitted["r"]);
    }

    [Fact]
    public void Json_WithoutObject_IsReported()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = llm.json(\"дай\")",
            Online(),
            Host(new FakeLlm("Извините, не могу.")));

        Assert.Equal(DiagnosticCodes.BadFileFormat, error.Code);
    }

    // --- схема ответа ---

    /// <summary>
    /// Схема приводит ответ к объявленным типам: модель пишет сумму строкой, и без приведения
    /// скрипт упал бы двумя строками ниже — на сложении, а не на разборе.
    /// </summary>
    [Fact]
    public void Json_Schema_CoercesTypes()
    {
        var llm = new FakeLlm("""{ "номер": "7", "сумма": "1 234,50", "срочно": "да", "дата": "2026-03-15" }""");

        RunResult result = Script.RunWith(Host(llm), """
            let счёт = llm.json("Разбери счёт", schema: { номер: "num", сумма: "dec", срочно: "bool", дата: "date" })

            emit номер = счёт.номер
            emit сумма = счёт.сумма
            emit срочно = счёт.срочно
            emit год = date.year(счёт.дата)
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(7.0, result.Emitted["номер"]);
        Assert.Equal(1234.50m, result.Emitted["сумма"]);
        Assert.Equal(true, result.Emitted["срочно"]);
        Assert.Equal(2026.0, result.Emitted["год"]);
    }

    /// <summary>Схема попадает в запрос словами: модели незачем угадывать, чего от неё ждут.</summary>
    [Fact]
    public void Json_Schema_IsSpelledOutInRequest()
    {
        var llm = new FakeLlm("""{ "сумма": 1 }""");

        _ = Script.RunWith(Host(llm), "emit r = llm.json(\"Разбери\", schema: { сумма: \"dec\" })", Online());

        Assert.Contains("точное число", llm.LastMessages[^1].Content?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Промах по схеме стоит одной повторной попытки, и в ней сказано, что именно не подошло.
    /// Цикла нет намеренно: каждый заход — оплаченный запрос.
    /// </summary>
    [Fact]
    public void Json_Schema_RepairsOnce()
    {
        var llm = new FakeLlm("""{ "тон": "хороший" }""", """{ "тон": "хороший", "оценка": 5 }""");

        RunResult result = Script.RunWith(Host(llm), """
            let разбор = llm.json("Разбери отзыв", schema: { тон: "str", оценка: "num" })

            emit оценка = разбор.оценка
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(5.0, result.Emitted["оценка"]);
        Assert.Equal(2, llm.Requests);
        Assert.Contains("оценка", llm.LastMessages[^1].Content?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_Schema_SecondMiss_IsReported()
    {
        var llm = new FakeLlm("""{ "тон": "хороший" }""", """{ "тон": "хороший" }""");

        Diagnostic error = Script.FailsWith(
            "emit r = llm.json(\"Разбери\", schema: { тон: \"str\", оценка: \"num\" })",
            Online(),
            Host(llm));

        Assert.Equal(DiagnosticCodes.BadFileFormat, error.Code);
        Assert.Equal(2, llm.Requests);
    }

    /// <summary>Пояснение сверх схемы не повод платить второй раз: лишние поля остаются.</summary>
    [Fact]
    public void Json_Schema_ExtraFields_AreKept()
    {
        var llm = new FakeLlm("""{ "тон": "хороший", "почему": "быстро привезли" }""");

        RunResult result = Script.RunWith(
            Host(llm),
            "emit r = llm.json(\"Разбери\", schema: { тон: \"str\" }).почему",
            Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal("быстро привезли", result.Emitted["r"]);
        Assert.Equal(1, llm.Requests);
    }

    // --- пакетная генерация ---

    [Fact]
    public void Map_AddsAnswerColumn()
    {
        var llm = new FakeLlm("чайник", "утюг");

        RunResult result = Script.RunWith(Host(llm), """
            let t = table.of({ name: ["первый", "второй"] }) |> llm.map(prompt: row => "Назови ${row.name}")

            emit колонки = len(table.columns(t))
            emit первый = t[0].answer
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(2.0, result.Emitted["колонки"]);
        Assert.Equal("чайник", result.Emitted["первый"]);
        Assert.Equal(2, llm.Requests);
    }

    /// <summary>Повторный прогон с теми же запросами не тратит ни одного вызова: ответы из кэша.</summary>
    [Fact]
    public void Map_SamePrompts_ComeFromCache()
    {
        var llm = new FakeLlm("а", "б");
        var options = Online();

        options.Cache = new MemoryStageCache();

        const string Source = """
            let t = table.of({ name: ["x", "y"] }) |> llm.map(prompt: row => "Опиши ${row.name}")

            emit ответ = t[1].answer
            """;

        RunResult first = Script.RunWith(Host(llm), Source, options);
        RunResult again = Script.RunWith(Host(llm), Source, options);

        Assert.True(again.Success, Script.Report(again));
        Assert.Equal(first.Emitted["ответ"], again.Emitted["ответ"]);
        Assert.Equal(2, llm.Requests);
    }

    /// <summary>
    /// Бюджет опыта кончился — опыт останавливается с тем, что успел, а не срывается отказом.
    /// </summary>
    [Fact]
    public void ExperimentUntil_Budget_StopsWithPartialResult()
    {
        var llm = new FakeLlm("1", "1", "1", "1", "1", "1", "1", "1") { Cost = 0.4m };

        RunResult result = Script.RunWith(Host(llm), """
            let итоги = exp.grid({ вариант: ["a"] })
                |> exp.run(p => { score: len(llm.ask("оцени")) }, repeat: 8, until: { cost: 1 })

            emit строк = len(итоги)
            emit причина = итоги[0].stopped
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(3.0, result.Emitted["строк"]);
        Assert.Equal("budget", result.Emitted["причина"]);
    }

    /// <summary>Потолок расходов прогона тоже останавливает опыт с частичным итогом.</summary>
    [Fact]
    public void ExperimentUntil_RunLimit_StopsWithPartialResult()
    {
        var llm = new FakeLlm("1", "1", "1", "1", "1", "1");

        RunResult result = Script.RunWith(Host(llm), """
            let итоги = exp.grid({ вариант: ["a"] })
                |> exp.run(p => { score: len(llm.ask("оцени")) }, repeat: 6, until: { cost: 100 })

            emit строк = len(итоги)
            emit причина = итоги[0].stopped
            """, Online(calls: 2));

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(2.0, result.Emitted["строк"]);
        Assert.Equal("budget", result.Emitted["причина"]);
    }

    // --- извлечение корпуса ---

    /// <summary>
    /// Извлечение сразу таблицей: строка на текст, колонка на поле схемы. Иначе корпус из
    /// десяти договоров пришлось бы собирать в таблицу руками, теряя связь строки с источником.
    /// </summary>
    [Fact]
    public void Extract_BuildsTableFromTexts()
    {
        var llm = new FakeLlm(
            """{ "срок": "2026-03-15", "сумма": "1 250 000,00" }""",
            """{ "срок": "2026-04-01", "сумма": "700 000,00" }""");

        RunResult result = Script.RunWith(Host(llm), """
            let тексты = ["первый договор", "второй договор"]
            let r = llm.extract(тексты, schema: { срок: "date", сумма: "dec" }, about: "договора")

            emit строк = len(r)
            emit итого = dec.sum(r["сумма"])
            emit источник = r[1].source
            emit год = date.year(r[0].срок)
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(2.0, result.Emitted["строк"]);
        Assert.Equal(1_950_000.00m, result.Emitted["итого"]);
        Assert.Equal(1.0, result.Emitted["источник"]);
        Assert.Equal(2026.0, result.Emitted["год"]);
        Assert.Equal(2, llm.Requests);
    }

    /// <summary>Чего в тексте нет, остаётся пропуском: выдуманное значение хуже отсутствующего.</summary>
    [Fact]
    public void Extract_MissingField_StaysEmpty()
    {
        var llm = new FakeLlm("""{ "срок": null }""");

        RunResult result = Script.RunWith(
            Host(llm),
            "emit пусто = llm.extract(\"договор\", schema: { срок: \"date\" })[0][\"срок\"] == none",
            Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(true, result.Emitted["пусто"]);
    }

    [Fact]
    public void Extract_EmptySchema_IsRejected()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = llm.extract([\"текст\"], schema: { })",
            Online(),
            Host(new FakeLlm("{}")));

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    /// <summary>Схема уходит в запрос словами: модель не угадывает, чего от неё ждут.</summary>
    [Fact]
    public void Extract_AsksForDeclaredFields()
    {
        var llm = new FakeLlm("""{ "сумма": 1 }""");

        _ = Script.RunWith(
            Host(llm),
            "emit r = llm.extract(\"текст\", schema: { сумма: \"dec\" }, about: \"акта\")",
            Online());

        string asked = llm.LastMessages[^1].Content?.ToString() ?? string.Empty;

        Assert.Contains("акта", asked, StringComparison.Ordinal);
        Assert.Contains("точное число", asked, StringComparison.Ordinal);
    }

    /// <summary>Ответ приводится к ближайшей метке: модель отвечает не тем словом, о котором просили.</summary>
    [Fact]
    public void Classify_MapsVerboseAnswerToLabel()
    {
        var llm = new FakeLlm("Это скорее положительный отзыв.");

        RunResult result = Script.RunWith(Host(llm), """
            emit метка = llm.classify("Всё понравилось", labels: ["положительный", "отрицательный"])
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal("положительный", result.Emitted["метка"]);
    }

    [Fact]
    public void Classify_UnrelatedAnswer_IsReported()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = llm.classify(\"текст\", labels: [\"да\", \"нет\"])",
            Online(),
            Host(new FakeLlm("не знаю")));

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    // --- эмбеддинги и поиск ---

    [Fact]
    public void Embed_ReturnsVector()
    {
        RunResult result = Script.RunWith(Host(embedder: new FakeEmbedder()),
            "emit длина = len(llm.embed(\"прокси и сеть\"))", Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(9.0, result.Emitted["длина"]);
    }

    [Fact]
    public void EmbedAll_ReturnsMatrixRowPerText()
    {
        RunResult result = Script.RunWith(Host(embedder: new FakeEmbedder()), """
            let m = llm.embed_all(["прокси", "матрица", "график"])

            emit строк = mat.rows(m)
            emit столбцов = mat.cols(m)
            """, Online());

        Assert.Equal(3.0, result.Emitted["строк"]);
        Assert.Equal(9.0, result.Emitted["столбцов"]);
    }

    [Fact]
    public void Search_Semantic_FindsRelevantDocument()
    {
        RunResult result = Script.RunWith(Host(embedder: new FakeEmbedder()), """
            let документы = [
                "Как настроить прокси в сети",
                "Умножение матриц и вектор",
                "Обучение модели на таблице"
            ]

            let индекс = search.of(документы)
            let найдено = индекс.query("прокси", top: 2)

            emit первый = найдено[0].doc
            emit вид = индекс.kind()
            emit размер = индекс.size()
            emit строк = len(найдено)
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(0.0, result.Emitted["первый"]);
        Assert.Equal("semantic", result.Emitted["вид"]);
        Assert.Equal(3.0, result.Emitted["размер"]);
        Assert.Equal(2.0, result.Emitted["строк"]);
    }

    /// <summary>Словесный индекс работает без эмбеддера и без сети — на нём же держится офлайн-режим.</summary>
    [Fact]
    public void Search_Words_NeedsNeitherEmbedderNorNetwork()
    {
        RunResult result = Script.RunWith(Host(), """
            let документы = ["настройка прокси", "умножение матриц", "обучение модели"]
            let индекс = search.of(документы, kind: "words")

            emit вид = индекс.kind()
            emit первый = индекс.query("прокси", top: 1)[0].text
            """);

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal("words", result.Emitted["вид"]);
        Assert.Equal("настройка прокси", result.Emitted["первый"]);
    }

    [Fact]
    public void Search_Semantic_WithoutEmbedder_SuggestsWords()
    {
        Diagnostic error = Script.FailsWith("emit r = search.of([\"а\", \"б\"])", Online(), Host(new FakeLlm()));

        Assert.Contains("kind: \"words\"", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_Context_GluesFoundFragments()
    {
        RunResult result = Script.RunWith(Host(embedder: new FakeEmbedder()), """
            let индекс = search.of(["про прокси", "про матрица"])

            emit фон = индекс.context("прокси", top: 1)
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal("[0] про прокси", result.Emitted["фон"]);
    }

    [Fact]
    public void Search_UnknownKind_IsReported()
    {
        Diagnostic error = Script.FailsWith(
            "emit r = search.of([\"а\"], kind: \"волшебный\")",
            Online(),
            Host(embedder: new FakeEmbedder()));

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("hybrid", error.Hint, StringComparison.Ordinal);
    }

    // --- секреты ---

    [Fact]
    public void Secrets_AreMaskedInTranscript()
    {
        var options = new RunOptions { Secrets = ["sk-очень-секретный-ключ"] };

        RunResult result = Script.Run("print(\"ключ: sk-очень-секретный-ключ\")\nemit r = 1", options);

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal("ключ: ***", result.Transcript[0]);
    }

    [Fact]
    public void Secrets_AreMaskedInFailureMessage()
    {
        var options = new RunOptions { Secrets = ["sk-очень-секретный-ключ"] };

        RunResult result = Script.Run("assert false, \"отказ с ключом sk-очень-секретный-ключ\"", options);

        Assert.False(result.Success);
        Assert.DoesNotContain("sk-очень", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("***", result.Error!.Message, StringComparison.Ordinal);
    }

    /// <summary>Короткое значение не маскируется: иначе весь вывод превратился бы в звёздочки.</summary>
    [Fact]
    public void Secrets_TooShortAreIgnored()
    {
        var mask = new SecretMask(["a", "1234567"]);

        Assert.True(mask.IsEmpty);
        Assert.Equal("1234567 и a", mask.Apply("1234567 и a"));
    }

    [Fact]
    public void Secrets_LongestIsMaskedFirst()
    {
        var mask = new SecretMask(["ключ-короткий", "ключ-короткий-и-длинный"]);

        Assert.Equal("***", mask.Apply("ключ-короткий-и-длинный"));
    }

    // --- профили ---

    [Fact]
    public void UntrustedProfile_DeniesNetworkAndWriting()
    {
        RunOptions options = RunProfiles.Untrusted("./workspace");

        Assert.False(options.Network.Enabled);
        Assert.True(options.Sandbox is WorkspaceSandbox { IsReadOnly: true });
        Assert.Equal(RunProfiles.UntrustedTimeout, options.Limits.Timeout);
        Assert.Contains("timeout", options.LockedOptions);
    }

    /// <summary>Недоверенный прогон не может поднять себе таймаут блоком options.</summary>
    [Fact]
    public void UntrustedProfile_LocksTimeout()
    {
        RunResult result = Script.Run("options { timeout: 10m }\nemit r = 1", RunProfiles.Untrusted());

        Assert.True(result.Success, Script.Report(result));

        // Подмена не молчаливая: автор скрипта узнаёт, что политика прогона ему не принадлежит.
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("закреплена хостом", StringComparison.Ordinal));
    }

    [Fact]
    public void TrustedProfile_AllowsNetwork()
    {
        Assert.True(RunProfiles.Trusted().Network.Enabled);
    }

    // --- найденное при проверке ---

    /// <summary>Пустое поле ответа не отбрасывает строку: остальные поля остаются.</summary>
    [Fact]
    public void Extract_NullField_KeepsOtherFields()
    {
        var llm = new FakeLlm("""{ "срок": null, "сумма": "1 000,00" }""");

        RunResult result = Script.RunWith(Host(llm), """
            let r = llm.extract(["договор"], schema: { срок: "date", сумма: "dec" })
            emit сумма = r[0].сумма
            emit срока_нет = r[0].срок == none
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(1000.00m, result.Emitted["сумма"]);
        Assert.Equal(true, result.Emitted["срока_нет"]);
    }

    /// <summary>Русская дата: день впереди месяца.</summary>
    [Fact]
    public void Json_RussianDate_IsDayFirst()
    {
        var llm = new FakeLlm("""{ "срок": "01.02.2026" }""");

        RunResult result = Script.RunWith(Host(llm), """
            emit месяц = date.month(llm.json("срок", schema: { срок: "date" }).срок)
            """, Online());

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(2.0, result.Emitted["месяц"]);
    }

}

/// <summary>Имена ролей сообщений — чтобы не повторять строковые литералы в проверках.</summary>
internal static class LLMMessageRoles
{
    public const string System = "system";
    public const string User = "user";
}
