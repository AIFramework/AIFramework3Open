using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Пространства <c>logic</c>, <c>siglab</c> и <c>explain</c>.
/// </summary>
/// <remarks>
/// Проверяется привязка, а не сами алгоритмы: попали ли аргументы туда, куда должны, и
/// означают ли поля результата написанное в их именах. Поэтому ожидания заданы величинами,
/// проверяемыми на бумаге, — задача о ложноположительных из учебника, безошибочный канал,
/// текст, целиком взятый из источника.
/// </remarks>
public sealed class LogicSignalExplainTests
{
    // --- логика ---

    /// <summary>
    /// Учебная задача: болезнь встречается у одного из ста, тест верен в 99 % случаев и даёт
    /// 5 % ложноположительных. При положительном тесте вероятность болезни — всего 16.7 %.
    /// </summary>
    [Fact]
    public void Logic_Bayes_SolvesFalsePositiveProblem()
    {
        RunResult result = Script.RunOk("""
            let после = logic.bayes(
                priors: { болен: 0.01, здоров: 0.99 },
                likelihoods: { болен: 0.99, здоров: 0.05 })

            emit болен = core.round(после.болен, digits: 4)
            emit здоров = core.round(после.здоров, digits: 4)
            emit сумма = core.round(после.болен + после.здоров, digits: 9)
            """);

        Assert.Equal(0.1667, (double)result.Emitted["болен"]!, 3);
        Assert.Equal(0.8333, (double)result.Emitted["здоров"]!, 3);
        Assert.Equal(1.0, result.Emitted["сумма"]);
    }

    /// <summary>Ненормированные априорные вероятности — ошибка, а не молчаливая нормировка.</summary>
    [Fact]
    public void Logic_Bayes_RejectsPriorsThatDoNotSumToOne()
    {
        Diagnostic error = Script.FailsWith("""
            emit r = logic.bayes(priors: { a: 0.5, b: 0.2 }, likelihoods: { a: 1, b: 1 })
            """);

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("единицу", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Опечатка в имени гипотезы не должна проходить молча.</summary>
    [Fact]
    public void Logic_Bayes_RejectsUnknownHypothesis()
    {
        Diagnostic error = Script.FailsWith("""
            emit r = logic.bayes(priors: { болен: 0.01, здоров: 0.99 }, likelihoods: { боленн: 0.99 })
            """);

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("боленн", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// «Сократ — человек», «человек — животное»: вывод обязан дать «Сократ — животное».
    /// </summary>
    [Fact]
    public void Logic_Ontology_InfersThroughHierarchy()
    {
        RunResult result = Script.RunOk("""
            let триплеты = table.of({
                subject: ["Сократ", "Человек", "Животное"],
                predicate: ["rdf:type", "rdfs:subClassOf", "rdfs:subClassOf"],
                object: ["Человек", "Животное", "Существо"]
            })

            let о = logic.ontology(триплеты)
            let выведено = о.infer()

            emit записано = о.size()
            emit про_сократа = len(о.about("Сократ"))
            emit выведено_всего = len(выведено)
            emit есть_вывод = len(выведено |> table.filter(t => t.subject == "Сократ")) > 0
            """);

        Assert.Equal(3.0, result.Emitted["записано"]);
        Assert.Equal(1.0, result.Emitted["про_сократа"]);
        Assert.True((double)result.Emitted["выведено_всего"]! > 0);
        Assert.Equal(true, result.Emitted["есть_вывод"]);
    }

    [Fact]
    public void Logic_Ontology_MissingColumn_IsReported()
    {
        Diagnostic error = Script.FailsWith("emit r = logic.ontology(table.of({ subject: [\"a\"] }))");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
        Assert.Contains("predicate", error.Hint, StringComparison.Ordinal);
    }

    // --- радиоканал ---

    /// <summary>Текст обязан пережить дорогу в биты и обратно без потерь.</summary>
    [Fact]
    public void Siglab_Bits_RoundTripKeepsText()
    {
        RunResult result = Script.RunOk("""
            let биты = siglab.to_bits("привет, мир")

            emit длина = len(биты)
            emit обратно = siglab.to_text(биты)
            """);

        // Каждый байт — восемь бит; кириллица в UTF-8 занимает по два байта.
        Assert.Equal(0.0, (double)result.Emitted["длина"]! % 8);
        Assert.Equal("привет, мир", result.Emitted["обратно"]);
    }

    [Fact]
    public void Siglab_ToText_RejectsRaggedBits()
    {
        Diagnostic error = Script.FailsWith("emit r = siglab.to_text(<1, 0, 1>)");

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
    }

    /// <summary>В канале без шума модуляция и демодуляция дают исходные биты.</summary>
    [Fact]
    public void Siglab_Ask_RoundTripIsErrorFree()
    {
        RunResult result = Script.RunOk("""
            let биты = siglab.to_bits("тест")
            let сигнал = siglab.ask(биты, carrier: 2000, fs: 48000, bit_duration: 0.002)
            let принято = siglab.ask_demod(сигнал, carrier: 2000, fs: 48000, bit_duration: 0.002)

            emit отсчётов = len(сигнал)
            emit ошибок = siglab.ber(биты, принято)
            emit текст = siglab.to_text(принято)
            """);

        Assert.Equal(0.0, result.Emitted["ошибок"]);
        Assert.Equal("тест", result.Emitted["текст"]);
        Assert.True((double)result.Emitted["отсчётов"]! > 0);
    }

    /// <summary>Несущая выше половины частоты дискретизации — ошибка, а не тихая ерунда.</summary>
    [Fact]
    public void Siglab_Ask_RejectsCarrierAboveNyquist()
    {
        Diagnostic error = Script.FailsWith("emit r = siglab.ask(<1, 0>, carrier: 30000, fs: 48000)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("Найквиста", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Siglab_Iq_RoundTripsThroughConstellation()
    {
        RunResult result = Script.RunOk("""
            let биты = siglab.to_bits("ab")
            let символы = siglab.iq(биты, kind: "qpsk")
            let принято = siglab.iq_bits(символы.i, символы.q, kind: "qpsk", bits: len(биты))

            emit символов = len(символы.i)
            emit на_символ = символы.бит_на_символ
            emit ошибок = siglab.ber(биты, принято)
            """);

        // Два бита на символ: шестнадцать бит дают восемь символов.
        Assert.Equal(2.0, result.Emitted["на_символ"]);
        Assert.Equal(8.0, result.Emitted["символов"]);
        Assert.Equal(0.0, result.Emitted["ошибок"]);
    }

    [Fact]
    public void Siglab_Constellation_HasExpectedSize()
    {
        RunResult result = Script.RunOk("""
            emit bpsk = len(siglab.constellation("bpsk").i)
            emit qpsk = len(siglab.constellation("qpsk").i)
            emit qam16 = len(siglab.constellation("qam16").i)
            """);

        Assert.Equal(2.0, result.Emitted["bpsk"]);
        Assert.Equal(4.0, result.Emitted["qpsk"]);
        Assert.Equal(16.0, result.Emitted["qam16"]);
    }

    [Fact]
    public void Siglab_UnknownModulation_IsReported()
    {
        Diagnostic error = Script.FailsWith("emit r = siglab.constellation(\"qam64\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("qam16", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Siglab_Ber_CountsMismatchedAndMissingBits()
    {
        Assert.Equal(0.0, Script.Number("siglab.ber(<1, 0, 1, 0>, <1, 0, 1, 0>)"));
        Assert.Equal(0.25, Script.Number("siglab.ber(<1, 0, 1, 0>, <1, 1, 1, 0>)"));

        // Недостающие биты — тоже ошибки: приёмник, потерявший половину пакета, не безупречен.
        Assert.Equal(0.5, Script.Number("siglab.ber(<1, 0, 1, 0>, <1, 0>)"));
    }

    [Fact]
    public void Siglab_Quadrature_ReturnsBothComponents()
    {
        RunResult result = Script.RunOk("""
            let t = signal.time(0.05, fs: 48000)
            let s = signal.sine(t, freq: 2000)
            let iq = siglab.quadrature(s, carrier: 2000, fs: 48000)

            emit i = len(iq.i)
            emit q = len(iq.q)
            emit задержка = iq.задержка >= 0
            """);

        Assert.Equal(result.Emitted["i"], result.Emitted["q"]);
        Assert.True((double)result.Emitted["i"]! > 0);
        Assert.Equal(true, result.Emitted["задержка"]);
    }

    [Fact]
    public void Siglab_Rrc_KernelIsSymmetricAndFinite()
    {
        RunResult result = Script.RunOk("""
            let ядро = siglab.rrc(symbol_period: 0.001, fs: 48000, roll_off: 0.35, span: 4)

            emit длина = len(ядро)
            emit симметрично = math.approx(ядро[0], ядро[len(ядро) - 1], eps: 1e-9)
            emit конечно = math.abs(vec.sum(ядро)) < 1000
            """);

        Assert.True((double)result.Emitted["длина"]! > 0);
        Assert.Equal(true, result.Emitted["симметрично"]);
        Assert.Equal(true, result.Emitted["конечно"]);
    }

    [Fact]
    public void Siglab_Agc_KeepsLengthAndTamesSpikes()
    {
        RunResult result = Script.RunOk("""
            options { seed: 4 }

            let тихо = signal.noise(2000, sigma: 0.01)
            let громко = signal.noise(2000, sigma: 10)
            let вход = vec.concat(тихо, громко)
            let выход = siglab.agc(вход)

            emit длина = len(выход) == len(вход)
            emit разброс_входа = stat.std(вход)
            emit разброс_выхода = stat.std(выход)
            """);

        Assert.Equal(true, result.Emitted["длина"]);

        // Регулировка на то и регулировка: разброс на выходе меньше, чем на входе.
        Assert.True((double)result.Emitted["разброс_выхода"]! < (double)result.Emitted["разброс_входа"]!);
    }

    // --- проверка ответа ---

    private const string Doc =
        "Прокси настраивается в разделе «Сеть»: адрес, порт и, при необходимости, логин с паролем. " +
        "Таймаут запроса задаётся в секундах, по умолчанию тридцать. " +
        "Ключ доступа хранится в переменной окружения и в текст скрипта не попадает.";

    /// <summary>Ответ, дословно взятый из источника, опирается на него полностью.</summary>
    [Fact]
    public void Explain_Grounded_IsHighForQuotedAnswer()
    {
        RunResult result = Script.RunOk($$"""
            let документ = "{{Doc}}"

            emit опора = explain.grounded(
                doc: документ,
                answer: "Таймаут запроса задаётся в секундах, по умолчанию тридцать.")
            """);

        Assert.Equal(1.0, (double)result.Emitted["опора"]!, 3);
    }

    /// <summary>Выдуманный ответ подтверждения не находит, и это видно числом.</summary>
    [Fact]
    public void Explain_Hallucination_IsHighForInventedAnswer()
    {
        RunResult result = Script.RunOk($$"""
            let документ = "{{Doc}}"

            emit выдумка = explain.hallucination(
                doc: документ,
                answer: "Лицензия продлевается через личный кабинет раз в три года.")
            """);

        Assert.True((double)result.Emitted["выдумка"]! > 0.9);
    }

    /// <summary>Опора и галлюцинация — дополняющие доли: вместе они дают единицу.</summary>
    [Fact]
    public void Explain_GroundedAndHallucination_SumToOne()
    {
        RunResult result = Script.RunOk($$"""
            let документ = "{{Doc}}"
            let ответ = "Ключ доступа хранится в переменной окружения. Обновляется он ежемесячно."

            emit сумма = core.round(
                explain.grounded(doc: документ, answer: ответ)
                + explain.hallucination(doc: документ, answer: ответ),
                digits: 9)
            """);

        Assert.Equal(1.0, result.Emitted["сумма"]);
    }

    [Fact]
    public void Explain_Support_PointsAtSourceFragment()
    {
        RunResult result = Script.RunOk($$"""
            let документ = "{{Doc}}"

            let подтверждения = explain.support(
                doc: документ,
                answer: "Ключ доступа хранится в переменной окружения и в текст скрипта не попадает.")

            emit строк = len(подтверждения)
            emit позиция = подтверждения[0].position
            emit есть_ключ = str.contains(подтверждения[0].support, "Ключ доступа")
            """);

        Assert.Equal(1.0, result.Emitted["строк"]);
        Assert.Equal(2.0, result.Emitted["позиция"]);
        Assert.Equal(true, result.Emitted["есть_ключ"]);
    }

    [Fact]
    public void Explain_Similarity_IsOneForSameText()
    {
        Assert.Equal(1.0, Script.Number("explain.similarity(\"один и тот же текст\", \"один и тот же текст\")"), 9);
        Assert.True(Script.Number("explain.similarity(\"кошка на окне\", \"поезд в депо\")") < 0.2);
    }

    [Fact]
    public void Explain_Blocks_SplitsBySentences()
    {
        RunResult result = Script.RunOk($$"""
            let документ = "{{Doc}}"

            emit блоков = len(explain.blocks(документ))
            """);

        Assert.Equal(3.0, result.Emitted["блоков"]);
    }

    [Fact]
    public void Explain_EmptySource_IsReported()
    {
        Diagnostic error = Script.FailsWith("emit r = explain.grounded(doc: \"\", answer: \"что-то\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }
}
