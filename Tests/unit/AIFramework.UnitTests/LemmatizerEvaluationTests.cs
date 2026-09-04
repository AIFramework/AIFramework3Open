using AI.Insights;
using AI.NLP.Evaluation;
using AI.NLP.Lemmatization;
using Xunit;
using Xunit.Abstractions;

namespace AIFramework.UnitTests;

/// <summary>
/// Проверка лемматизаторов на эталонном корпусе. Тесты закрепляют измеренное качество:
/// пока числа не зафиксированы, любое изменение правил остаётся изменением вслепую.
/// </summary>
public class LemmatizerEvaluationTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>Создаёт набор тестов</summary>
    /// <param name="output">Вывод для отчёта</param>
    public LemmatizerEvaluationTests(ITestOutputHelper output) => _output = output;

    #region Корпус

    [Fact]
    public void Corpus_IsLoadedFromAssembly()
    {
        LemmaCorpus corpus = LemmaCorpus.Russian;

        Assert.True(corpus.Count > 200, $"В корпусе всего {corpus.Count} записей");
        Assert.Contains("NOUN", corpus.PartsOfSpeech);
        Assert.Contains("VERB", corpus.PartsOfSpeech);
        Assert.Contains("ADJ", corpus.PartsOfSpeech);
    }

    [Fact]
    public void Corpus_HasThreeSections()
    {
        LemmaCorpus corpus = LemmaCorpus.Russian;

        Assert.True(corpus.Section(CorpusSection.Base).Count > 150);
        Assert.True(corpus.Section(CorpusSection.Suppletive).Count > 20);
        Assert.True(corpus.Section(CorpusSection.Ambiguous).Count > 5);
    }

    [Fact]
    public void Corpus_AmbiguousEntries_CarryExplanation()
    {
        foreach (LemmaSample sample in LemmaCorpus.Russian.Section(CorpusSection.Ambiguous).Samples)
            Assert.False(string.IsNullOrWhiteSpace(sample.Note), $"У записи «{sample.Form}» нет пояснения");
    }

    [Fact]
    public void Corpus_ParsesCustomText()
    {
        string text = string.Join("\n",
            "# комментарий",
            "столы\tстол\tNOUN\tbase",
            "людей\tчеловек\tNOUN\tsuppletive");

        LemmaCorpus corpus = LemmaCorpus.Parse(text);

        Assert.Equal(2, corpus.Count);
        Assert.Equal(CorpusSection.Suppletive, corpus.Samples[1].Section);
    }

    #endregion

    #region Измеренное качество: правила без разбора части речи

    [Fact]
    public void RussianLemmatizer_QualityIsMeasuredAndRecorded()
    {
        LemmatizationReport report = LemmatizerEvaluation.Evaluate(new RussianLemmatizer());

        _output.WriteLine(report.Interpret().ToLlmText());

        // Замер 2026-09-04: 146 из 239 — 61.1 %, на регулярных формах 55.3 %.
        // Падение ниже означает, что правки правил сделали хуже.
        Assert.True(report.Accuracy >= 0.61,
            $"Общая точность упала до {report.Accuracy:P1}");
        Assert.True(report.AccuracyFor(CorpusSection.Base) >= 0.55,
            $"Точность на регулярных формах упала до {report.AccuracyFor(CorpusSection.Base):P1}");
    }

    [Fact]
    public void RussianLemmatizer_HasNoNounRules()
    {
        LemmatizationReport report = LemmatizerEvaluation.Evaluate(new RussianLemmatizer());

        // Средняя точность скрывает главное: глагол и прилагательное разобраны почти
        // безупречно, а существительное — почти никогда. Правил склонения в этом
        // лемматизаторе нет вовсе; те немногие, что берутся, взяты списком супплетивов.
        Assert.True(report.AccuracyFor("ADJ") >= 0.95, $"ADJ: {report.AccuracyFor("ADJ"):P1}");
        Assert.True(report.AccuracyFor("VERB") >= 0.90, $"VERB: {report.AccuracyFor("VERB"):P1}");
        Assert.True(report.AccuracyFor("NOUN") < 0.10, $"NOUN: {report.AccuracyFor("NOUN"):P1}");
    }

    [Fact]
    public void RussianLemmatizer_SuppletiveFormsAreCarriedByTheDictionary()
    {
        LemmatizationReport report = LemmatizerEvaluation.Evaluate(new RussianLemmatizer());

        // Супплетивные формы взяты лучше регулярных: работает словарь закрытых классов,
        // а не правила. Это и есть признак того, что пробел именно в правилах.
        Assert.Equal(1.0, report.AccuracyFor(CorpusSection.Suppletive));
        Assert.True(report.AccuracyFor(CorpusSection.Suppletive) > report.AccuracyFor(CorpusSection.Base));
    }

    [Fact]
    public void RussianLemmatizer_FailuresMostlyReturnTheOriginalWord()
    {
        LemmatizationReport report = LemmatizerEvaluation.Evaluate(new RussianLemmatizer());

        // Отказ безопаснее неверного разбора: слово остаётся узнаваемым
        Assert.True(report.UnchangedShare > 0.9,
            $"Доля отказов среди ошибок {report.UnchangedShare:P1}: разборы стали агрессивнее");
    }

    #endregion

    #region Измеренное качество: разбор с определением части речи

    [Fact]
    public void MorphologicalLemmatizer_IsMeasurablyBetter()
    {
        LemmatizationReport rules = LemmatizerEvaluation.Evaluate(new RussianLemmatizer());
        LemmatizationReport morph = LemmatizerEvaluation.Evaluate(new MorphologicalLemmatizer());

        _output.WriteLine(morph.Interpret().ToLlmText());

        // Замер 2026-09-04: 198 из 239 — 82.8 % против 61.1 % у правил без разбора
        // части речи. Прибавка целиком приходится на существительные.
        Assert.True(morph.Accuracy >= 0.82, $"Точность упала до {morph.Accuracy:P1}");
        Assert.True(morph.Accuracy > rules.Accuracy + 0.2,
            $"Разбор с частью речи даёт {morph.Accuracy:P1} против {rules.Accuracy:P1}");
        Assert.True(morph.AccuracyFor("NOUN") >= 0.60, $"NOUN: {morph.AccuracyFor("NOUN"):P1}");
    }

    [Fact]
    public void MorphologicalLemmatizer_KeepsVerbsAndAdjectives()
    {
        LemmatizationReport rules = LemmatizerEvaluation.Evaluate(new RussianLemmatizer());
        LemmatizationReport morph = LemmatizerEvaluation.Evaluate(new MorphologicalLemmatizer());

        // Существительные добавлены не за счёт остальных: глагол и прилагательное
        // по-прежнему разбирает та же проверенная таблица правил.
        Assert.Equal(rules.AccuracyFor("VERB"), morph.AccuracyFor("VERB"), 12);
        Assert.Equal(rules.AccuracyFor("ADJ"), morph.AccuracyFor("ADJ"), 12);
    }

    [Fact]
    public void MorphologicalLemmatizer_ErrorsAreMostlyWrongParse_NotRefusal()
    {
        LemmatizationReport morph = LemmatizerEvaluation.Evaluate(new MorphologicalLemmatizer());

        foreach (LemmaError error in morph.Errors)
            _output.WriteLine(error.ToString());

        // Обратная сторона правил склонения: «книгой» становится «книг», а не остаётся
        // нетронутым. Ошибка теперь чаще порча, чем отказ, и об этом честнее знать.
        Assert.True(morph.UnchangedShare < 0.5,
            $"Доля отказов среди ошибок {morph.UnchangedShare:P1}");
    }

    [Fact]
    public void Lemmatizers_AreIdempotentOnCorpus()
    {
        ILemmatizer[] lemmatizers = { new RussianLemmatizer(), new MorphologicalLemmatizer() };

        foreach (ILemmatizer lemmatizer in lemmatizers)
        {
            foreach (LemmaSample sample in LemmaCorpus.Russian.Samples)
            {
                string once = lemmatizer.Lemmatize(sample.Form);
                string twice = lemmatizer.Lemmatize(once);

                Assert.Equal(once, twice);
            }
        }
    }

    [Fact]
    public void IdentityLemmatizer_IsTheBaseline()
    {
        // Лемматизатор, ничего не делающий, задаёт нижнюю границу: с ней сравнивается всё остальное
        LemmatizationReport baseline = LemmatizerEvaluation.Evaluate(new IdentityLemmatizer());
        LemmatizationReport rules = LemmatizerEvaluation.Evaluate(new RussianLemmatizer());

        _output.WriteLine($"База: {baseline}, правила: {rules}");

        Assert.True(rules.Accuracy > baseline.Accuracy + 0.3,
            $"Правила дают {rules.Accuracy:P1} против {baseline.Accuracy:P1} у пустого лемматизатора");
    }

    [Fact]
    public void Comparison_DetectsDifferenceBetweenLemmatizers()
    {
        (double difference, IReadOnlyList<string> diverged) =
            LemmatizerEvaluation.Compare(new IdentityLemmatizer(), new RussianLemmatizer());

        Assert.True(difference > 0);
        Assert.NotEmpty(diverged);
    }

    #endregion

    #region Отчёт

    [Fact]
    public void Report_ExplainsCostOfMissingDictionary()
    {
        Interpretation interpretation = LemmatizerEvaluation.Evaluate(new RussianLemmatizer()).Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "Точность");
        Assert.Contains(interpretation.Metrics, m => m.Name == "Супплетивные формы");
        Assert.Contains(interpretation.Findings, f => f.Contains("словар", StringComparison.Ordinal));
        Assert.Contains(interpretation.Warnings, w => w.Contains("вручную", StringComparison.Ordinal));
    }

    [Fact]
    public void Report_NamesThePartOfSpeechThatIsNotParsed()
    {
        Interpretation interpretation = LemmatizerEvaluation.Evaluate(new RussianLemmatizer()).Interpret();

        // Отчёт обязан называть провал по имени: «NOUN (4 из 91)», а не прятать его
        // в средней точности
        Assert.Contains(interpretation.Findings, f => f.Contains("NOUN (", StringComparison.Ordinal));
    }

    [Fact]
    public void Report_CountsMatchCorpusSize()
    {
        LemmatizationReport report = LemmatizerEvaluation.Evaluate(new RussianLemmatizer());
        int expected = LemmaCorpus.Russian.Count - LemmaCorpus.Russian.Section(CorpusSection.Ambiguous).Count;

        Assert.Equal(expected, report.Total);
        Assert.Equal(report.Total - report.Correct, report.Errors.Count);
    }

    #endregion
}
