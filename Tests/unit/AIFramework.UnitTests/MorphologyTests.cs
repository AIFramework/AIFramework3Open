using AI.Insights;
using AI.NLP.Evaluation;
using AI.NLP.Lemmatization;
using AI.NLP.Morphology;
using Xunit;
using Xunit.Abstractions;

namespace AIFramework.UnitTests;

/// <summary>
/// Проверка морфологического разбора: определитель части речи, правила склонения
/// существительных и словарь закрытых классов.
/// </summary>
public class MorphologyTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>Создаёт набор тестов</summary>
    /// <param name="output">Вывод для отчёта</param>
    public MorphologyTests(ITestOutputHelper output) => _output = output;

    #region Словарь закрытых классов

    [Theory]
    [InlineData("меня", "я", PartOfSpeech.Pronoun)]
    [InlineData("нам", "мы", PartOfSpeech.Pronoun)]
    [InlineData("трёх", "три", PartOfSpeech.Numeral)]
    [InlineData("пятью", "пять", PartOfSpeech.Numeral)]
    [InlineData("шёл", "идти", PartOfSpeech.Verb)]
    [InlineData("людей", "человек", PartOfSpeech.Noun)]
    [InlineData("через", "через", PartOfSpeech.Preposition)]
    [InlineData("чтобы", "чтобы", PartOfSpeech.Conjunction)]
    [InlineData("лишь", "лишь", PartOfSpeech.Particle)]
    [InlineData("поэтому", "поэтому", PartOfSpeech.Adverb)]
    public void ClosedClasses_AreKnownExactly(string form, string lemma, PartOfSpeech expected)
    {
        Assert.True(RussianClosedClassLexicon.TryLookup(form, out MorphAnalysis analysis));
        Assert.Equal(lemma, analysis.Lemma);
        Assert.Equal(expected, analysis.PartOfSpeech);
    }

    [Fact]
    public void ClosedClasses_AreNormalized()
    {
        // Вход разбора приведён к «е», значит и ключи словаря должны быть такими:
        // иначе формы с «ё» недостижимы
        Assert.True(RussianClosedClassLexicon.TryLookup("ТРЁХ", out MorphAnalysis upper));
        Assert.True(RussianClosedClassLexicon.TryLookup("трех", out MorphAnalysis noYo));

        Assert.Equal("три", upper.Lemma);
        Assert.Equal(upper, noYo);
    }

    #endregion

    #region Определитель части речи

    [Theory]
    [InlineData("столами", PartOfSpeech.Noun)]
    [InlineData("решением", PartOfSpeech.Noun)]
    [InlineData("конём", PartOfSpeech.Noun)]
    [InlineData("дому", PartOfSpeech.Noun)]
    [InlineData("читал", PartOfSpeech.Verb)]
    [InlineData("читает", PartOfSpeech.Verb)]
    [InlineData("говорит", PartOfSpeech.Verb)]
    [InlineData("учился", PartOfSpeech.Verb)]
    [InlineData("читающий", PartOfSpeech.Verb)]
    [InlineData("построенный", PartOfSpeech.Verb)]
    [InlineData("красивого", PartOfSpeech.Adjective)]
    [InlineData("синяя", PartOfSpeech.Adjective)]
    [InlineData("быстро", PartOfSpeech.Adverb)]
    [InlineData("2026", PartOfSpeech.Numeral)]
    public void Tagger_RecognizesOpenClasses(string word, PartOfSpeech expected)
        => Assert.Equal(expected, RussianPosTagger.Instance.Tag(word));

    [Fact]
    public void Tagger_PrefersNounForAmbiguousEndings()
    {
        // «-ом» и «-ем» принадлежат и существительному, и глаголу. Выбрано
        // существительное: глагольное правило превратило бы «конём» в «конать».
        Assert.Equal(PartOfSpeech.Noun, RussianPosTagger.Instance.Tag("конем"));
        Assert.Equal(PartOfSpeech.Noun, RussianPosTagger.Instance.Tag("столом"));

        // Глагол при этом не теряется: у него окончание длиннее и потому выигрывает
        Assert.Equal(PartOfSpeech.Verb, RussianPosTagger.Instance.Tag("читаем"));
    }

    [Fact]
    public void Tagger_DoesNotTakeNounsEndingInSyaForVerbs()
    {
        // Возвратный постфикс снимается только тогда, когда под ним глагольная форма
        Assert.Equal(PartOfSpeech.Verb, RussianPosTagger.Instance.Tag("умывался"));
        Assert.Equal(PartOfSpeech.Noun, RussianPosTagger.Instance.Tag("карася"));
    }

    [Fact]
    public void Tagger_TagsSequence()
    {
        IReadOnlyList<PartOfSpeech> tags =
            RussianPosTagger.Instance.Tag(new[] { "я", "читал", "книгу" });

        Assert.Equal(new[] { PartOfSpeech.Pronoun, PartOfSpeech.Verb, PartOfSpeech.Noun }, tags);
    }

    [Theory]
    [InlineData(PartOfSpeech.Noun, "NOUN")]
    [InlineData(PartOfSpeech.Verb, "VERB")]
    [InlineData(PartOfSpeech.Preposition, "PREP")]
    public void PartOfSpeechCodes_RoundTrip(PartOfSpeech pos, string code)
    {
        Assert.Equal(code, pos.ToCode());
        Assert.Equal(pos, PartOfSpeechCodes.Parse(code));
    }

    [Fact]
    public void PartOfSpeechCodes_UnderstandUniversalDependencies()
    {
        // Разметка UD называет предлог ADP, а частицу PART
        Assert.Equal(PartOfSpeech.Preposition, PartOfSpeechCodes.Parse("ADP"));
        Assert.Equal(PartOfSpeech.Particle, PartOfSpeechCodes.Parse("PART"));
        Assert.Equal(PartOfSpeech.Unknown, PartOfSpeechCodes.Parse("что-то своё"));
    }

    #endregion

    #region Правила склонения

    [Theory]
    [InlineData("столами", "стол")]
    [InlineData("столов", "стол")]
    [InlineData("вопроса", "вопрос")]
    [InlineData("городе", "город")]
    [InlineData("решением", "решение")]
    [InlineData("решений", "решение")]
    [InlineData("дверью", "дверь")]
    [InlineData("дверям", "дверь")]
    [InlineData("ночей", "ночь")]
    public void NounInflection_RestoresNominative(string form, string expected)
        => Assert.Equal(expected, RussianNounInflection.ToNominative(form));

    [Theory]
    [InlineData("окна", "окно")]
    [InlineData("окном", "окно")]
    [InlineData("окну", "окно")]
    public void NounInflection_UsesPhonotacticsForNeuter(string form, string expected)
    {
        // «окн» словом быть не может: в конце русского слова нет сочетания «шумный + н».
        // Значит основа среднего рода, и нулевого окончания здесь не бывает.
        Assert.False(RussianPhonetics.CanEndWord("окн"));
        Assert.True(RussianPhonetics.CanEndWord("стол"));
        Assert.Equal(expected, RussianNounInflection.ToNominative(form));
    }

    [Theory]
    [InlineData("дня", "день")]
    [InlineData("дню", "день")]
    [InlineData("днем", "день")]
    [InlineData("дней", "день")]
    [InlineData("дни", "день")]
    public void NounInflection_RestoresFillVowel(string form, string expected)
        => Assert.Equal(expected, RussianNounInflection.ToNominative(form));

    [Theory]
    [InlineData("книга")]
    [InlineData("окно")]
    [InlineData("стол")]
    [InlineData("день")]
    [InlineData("история")]
    [InlineData("решение")]
    public void NounInflection_IsIdempotent(string word)
    {
        string once = RussianNounInflection.ToNominative(word);

        Assert.Equal(once, RussianNounInflection.ToNominative(once));
    }

    [Fact]
    public void NounInflection_LeavesFeminineStemWithoutEnding()
    {
        // Сознательная потеря: «книгой» и «столом» устроены одинаково, и по одной
        // словоформе род не виден. Выбрано нулевое окончание — тогда все формы слова
        // сходятся к одному ключу и разбор остаётся устойчивым.
        Assert.Equal("книг", RussianNounInflection.ToNominative("книгой"));
        Assert.Equal("книг", RussianNounInflection.ToNominative("книгам"));
        Assert.Equal("книг", RussianNounInflection.ToNominative("книг"));
    }

    #endregion

    #region Разбор с определением части речи

    [Theory]
    [InlineData("конём", "конь")]
    [InlineData("поэтому", "поэтому")]
    [InlineData("столами", "стол")]
    [InlineData("решением", "решение")]
    public void MorphologicalLemmatizer_FixesDamageDoneByBlindRules(string form, string expected)
    {
        // Ровно эти слова портил разбор без части речи: «конать», «поэтый», «решениать»
        Assert.Equal(expected, MorphologicalLemmatizer.Instance.Lemmatize(form));
    }

    [Fact]
    public void MorphologicalLemmatizer_ReturnsPartOfSpeechToo()
    {
        MorphAnalysis analysis = MorphologicalLemmatizer.Instance.Analyze("городами");

        Assert.Equal("город", analysis.Lemma);
        Assert.Equal(PartOfSpeech.Noun, analysis.PartOfSpeech);
    }

    [Theory]
    [InlineData("работали", "работать")]
    [InlineData("учился", "учиться")]
    [InlineData("красивого", "красивый")]
    [InlineData("большим", "большой")]
    public void MorphologicalLemmatizer_KeepsVerbAndAdjectiveRules(string form, string expected)
        => Assert.Equal(expected, MorphologicalLemmatizer.Instance.Lemmatize(form));

    [Fact]
    public void MorphologicalLemmatizer_LemmatizesSentence()
    {
        string result = MorphologicalLemmatizer.Instance.LemmatizeSentence("Я читал книги в городах.");

        Assert.Equal("я читать книг в город.", result);
    }

    #endregion

    #region Измеренное качество разметки

    [Fact]
    public void PosTagger_QualityIsMeasuredAndRecorded()
    {
        PosTaggingReport report = PosTaggerEvaluation.Evaluate(new RussianPosTagger());

        _output.WriteLine(report.Interpret().ToLlmText());

        foreach (PosError error in report.Errors)
            _output.WriteLine(error.ToString());

        // Замер 2026-09-04: 230 из 239 — 96.2 %, F-мера 0.975
        Assert.True(report.Accuracy >= 0.96, $"Точность разметки {report.Accuracy:P1}");
        Assert.True(report.FMeasure >= 0.97, $"F-мера {report.FMeasure:F3}");
    }

    [Fact]
    public void PosTagger_RemainingErrorsAreHomonymy()
    {
        PosTaggingReport report = PosTaggerEvaluation.Evaluate(new RussianPosTagger());

        // Оставшиеся ошибки — не недоработка правил, а слова, часть речи которых
        // без соседей не определяется: «днём» (наречие или существительное),
        // «дела» (существительное или глагол), «читая» (деепричастие или прилагательное).
        Assert.True(report.Errors.Count <= 10, $"Ошибок стало {report.Errors.Count}");
        Assert.Contains(report.Errors, e => e.Form == "днём");
        Assert.Contains(report.Errors, e => e.Form == "дела");
    }

    [Fact]
    public void PosTagger_ConfusionMatrixMatchesCounts()
    {
        PosTaggingReport report = PosTaggerEvaluation.Evaluate(new RussianPosTagger());

        double sum = 0;
        for (int i = 0; i < report.Labels.Count; i++)
            for (int j = 0; j < report.Labels.Count; j++)
                sum += report.Confusion[i, j];

        Assert.Equal(report.Total, (int)sum);
        Assert.Equal(report.Total - report.Correct, report.Errors.Count);
    }

    [Fact]
    public void PosTaggingReport_ExplainsWhatItCannotDo()
    {
        Interpretation interpretation = PosTaggerEvaluation.Evaluate(new RussianPosTagger()).Interpret();

        Assert.Contains(interpretation.Metrics, m => m.Name == "F-мера");
        Assert.Contains(interpretation.Findings, f => f.Contains("путаются", StringComparison.Ordinal));
        Assert.Contains(interpretation.Warnings, w => w.Contains("без соседей", StringComparison.Ordinal));
    }

    #endregion
}
