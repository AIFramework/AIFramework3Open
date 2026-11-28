using AI.DataPrepaire.NLPUtils.RegexpNLP;
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Разбивка на предложения: одно правило конца, одна таблица сокращений,
/// NER защищает только точки внутри сущности.
/// </summary>
public class SentencesTokenizerTests
{
    private const string Next = "Следующее предложение.";

    private static readonly SentencesTokenizer Tokenizer = new();

    // Образец каждого NER-процессора: сущность стоит в конце предложения перед «. Следующее».
    // Новый процессор без образца роняет EveryNerProcessor_HasSample.
    private static readonly Dictionary<string, (string Sentence, string Entity)> NerSamples = new()
    {
        ["TimeProcessor"] = ("Встреча назначена на 22:00.", "22:00"),
        ["EmailAdressProcessor"] = ("Пишите на ivan.petrov@mail.ru.", "ivan.petrov@mail.ru"),
        ["SiteAdressProcessor"] = ("Документ лежит на https://example.com/docs/v1.2/index.html.", "https://example.com/docs/v1.2/index.html"),
        ["AdressProcessor"] = ("Склад: ул. Заводская, д. 7, кв. 12.", "ул. Заводская, д. 7, кв. 12"),
        ["PhoneNerProcessor"] = ("Звоните +7 999 123-45-67.", "+7 999 123-45-67"),
        ["NameRusNerProcessor"] = ("Договор подписал И. И. Иванов.", "И. И. Иванов"),
        ["OrderNumberNerProcessor"] = ("Требования изложены в разделе 7.4.1.", "7.4.1."),
    };

    public static IEnumerable<object[]> NerProcessorNames() =>
        typeof(RegexNer).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(RegexNer)) && !t.IsAbstract)
            .Select(t => new object[] { t.Name });

    // ----------------------------- NER-процессоры -----------------------------

    [Theory]
    [MemberData(nameof(NerProcessorNames))]
    public void EveryNerProcessor_HasSample(string processor) =>
        Assert.True(NerSamples.ContainsKey(processor), $"Нет образца сущности для {processor}");

    [Theory]
    [MemberData(nameof(NerProcessorNames))]
    public void NerEntityAtSentenceEnd_GivesBoundary(string processor)
    {
        var (sentence, entity) = NerSamples[processor];
        string text = sentence + " " + Next;
        var ner = CreateProcessor(processor);
        var spans = ner.FindEntities(text).ToList();

        Assert.Contains(spans, s => text.Substring(s.Start, s.Length).Contains(entity));
        Assert.Equal(new[] { sentence, Next }, Tokenizer.Tokenize(text, spans));
        Assert.Equal(new[] { sentence, Next }, Tokenizer.TokenizeWithNer(text));
    }

    [Theory]
    [MemberData(nameof(NerProcessorNames))]
    public void NerEntity_HasNoBoundaryInside(string processor)
    {
        string text = NerSamples[processor].Sentence + " " + Next;
        var ner = CreateProcessor(processor);
        var spans = ner.FindEntities(text).ToList();
        var boundaries = Boundaries(Tokenizer.Tokenize(text, spans, withTrim: false));

        foreach (var (start, length) in spans)
            Assert.DoesNotContain(boundaries, b => b > start && b < start + length - 1);
    }

    [Fact]
    public void EntityProtectsInnerDot_ButNotItsLastDot()
    {
        const string text = "Иванов. Петров пришёл.";

        Assert.Single(Tokenizer.Tokenize(text, new[] { (0, 14) }));
        Assert.Equal(new[] { "Иванов.", "Петров пришёл." }, Tokenizer.Tokenize(text, new[] { (0, 7) }));
    }

    [Theory]
    [InlineData("Насос марки НК-40. Срок службы 10 лет.", 2)]
    [InlineData("См. раздел 7.4. Итог проверки ниже.", 2)]
    [InlineData("Согласно п. 3.2. настоящего договора.", 1)]
    [InlineData("1. Предмет договора. 1.1. Поставщик обязуется.", 2)]
    public void NumberDot_EndsBeforeCapitalOnly(string text, int count) =>
        Assert.Equal(count, Tokenizer.TokenizeWithNer(text).Count);

    // ----------------------------- Сокращения -----------------------------

    public static IEnumerable<object[]> Abbreviations() =>
        SentenceAbbreviations.Entries.Select(e => new object[] { e.Abbreviation, e.Class });

    [Theory]
    [MemberData(nameof(Abbreviations))]
    public void Abbreviation_EndsByItsClass(string abbreviation, AbbreviationClass cls)
    {
        string shown = abbreviation.TrimEnd('.').Replace(".", ". ") + ".";

        int afterWord = Tokenizer.Tokenize($"Слово {shown} Далее текст.").Count;
        int afterNumber = Tokenizer.Tokenize($"Всего 5 {shown} Далее текст.").Count;
        int lowerNext = Tokenizer.Tokenize($"Всего 5 {shown} далее текст.").Count;

        Assert.Equal(cls == AbbreviationClass.MayEnd ? 2 : 1, afterWord);
        Assert.Equal(cls == AbbreviationClass.NeverEnd ? 1 : 2, afterNumber);
        Assert.Equal(1, lowerNext);
    }

    [Fact]
    public void AbbreviationTable_HasNoDuplicatesAndEveryEntryHasClass()
    {
        var entries = SentenceAbbreviations.Entries;

        Assert.Equal(entries.Count, entries.Select(e => e.Abbreviation.ToLowerInvariant()).Distinct().Count());
        Assert.All(entries, e => Assert.True(Enum.IsDefined(e.Class), $"Нет класса у {e.Abbreviation}"));
        Assert.All(entries, e => Assert.Matches(@"^(\p{Ll}+\.)+$", e.Abbreviation));
    }

    [Theory]
    [InlineData("Оплата 5 000 руб. Далее идёт текст.", 2)]
    [InlineData("Цена 4 500 руб. за единицу.", 1)]
    [InlineData("См. раздел 7.4 ст. 12. Итог.", 2)]
    [InlineData("Не позднее 15.12.2026 г. включительно.", 1)]
    [InlineData("Срок — до 01.02.2027 г. Стороны согласны.", 2)]
    [InlineData("Адрес: г. Тула, ул. Заводская.", 1)]
    [InlineData("Председатель — А. С. Петров. Совещание открыто.", 2)]
    [InlineData("Перечень: столы, стулья и т. д. Далее цены.", 2)]
    [InlineData("Платят в срок, т. е. Заказчик не ждёт.", 1)]
    public void AbbreviationsInContext(string text, int count) =>
        Assert.Equal(count, Tokenizer.TokenizeWithNer(text).Count);

    [Fact]
    public void UserAbbreviations_NeverEnd()
    {
        const string text = "Цена с НДС вкл. Налог уплачен.";

        Assert.Equal(2, Tokenizer.Tokenize(text).Count);
        Assert.Single(new SentencesTokenizer(new[] { "вкл." }).Tokenize(text));
    }

    // ----------------------------- Знаки конца -----------------------------

    [Theory]
    [InlineData("Ответ… Да.", new[] { "Ответ…", "Да." })]
    [InlineData("Ответ... Да.", new[] { "Ответ...", "Да." })]
    [InlineData("Я думал… а потом решил.", new[] { "Я думал… а потом решил." })]
    [InlineData("Я думал... а потом решил.", new[] { "Я думал... а потом решил." })]
    [InlineData("Отложен?! Нет, рассмотрен.", new[] { "Отложен?!", "Нет, рассмотрен." })]
    [InlineData("Спасибо!!! Пока.", new[] { "Спасибо!!!", "Пока." })]
    [InlineData("«Готово.» Потом.", new[] { "«Готово.»", "Потом." })]
    [InlineData("(Так решили.) Далее.", new[] { "(Так решили.)", "Далее." })]
    [InlineData("«Готово?» — спросил он. Ладно.", new[] { "«Готово?» — спросил он.", "Ладно." })]
    [InlineData("привет. как дела? норм.", new[] { "привет.", "как дела?", "норм." })]
    public void Terminators(string text, string[] expected) =>
        Assert.Equal(expected, Tokenizer.TokenizeWithNer(text));

    [Fact]
    public void WithoutTrim_PiecesRebuildText()
    {
        const string text = "Привет! Как дела?  Хорошо... Да.\n";

        var pieces = Tokenizer.TokenizeWithNer(text, withTrim: false);

        Assert.Equal(4, pieces.Count);
        Assert.Equal(text, string.Concat(pieces));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyText_GivesNoSentences(string text) =>
        Assert.Empty(Tokenizer.TokenizeWithNer(text));

    private static RegexNer CreateProcessor(string name) =>
        (RegexNer)Activator.CreateInstance(typeof(RegexNer).Assembly.GetTypes().Single(t => t.Name == name))!;

    private static List<int> Boundaries(List<string> pieces)
    {
        var result = new List<int>();
        int position = 0;
        foreach (string piece in pieces)
            result.Add(position += piece.Length);
        return result;
    }
}
