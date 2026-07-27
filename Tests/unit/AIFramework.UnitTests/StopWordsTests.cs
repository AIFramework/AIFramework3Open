using AI.NLP;
using System;
using System.Linq;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Стоп-лист русского языка. Раньше <see cref="ProbabilityDictionary.stop"/> был пустым
/// массивом: фильтрация была объявлена, но не работала, и служебные слова («в», «на»,
/// «что», «который») попадали в индекс наравне со значимыми.
/// </summary>
public class StopWordsTests
{
    // ----------------------------- Фильтрация -----------------------------

    [Fact]
    public void Service_Words_Are_Removed_From_Tokens()
    {
        var words = ProbabilityDictionary.GetWords(
            "Неустойка за просрочку платежа по договору не начисляется", IsStem: false);

        Assert.DoesNotContain("за", words);
        Assert.DoesNotContain("по", words);
        Assert.DoesNotContain("не", words);

        // Значимые слова остаются.
        Assert.Contains("неустойка", words);
        Assert.Contains("просрочку", words);
        Assert.Contains("договору", words);
    }

    [Fact]
    public void Capitalised_Service_Words_Are_Removed_Too()
    {
        var words = ProbabilityDictionary.GetWords("В договоре. Что именно указано?", IsStem: false);

        Assert.DoesNotContain("в", words);
        Assert.DoesNotContain("что", words);
        Assert.Contains("договоре", words);
    }

    [Fact]
    public void Query_Of_Only_Service_Words_Yields_No_Terms()
    {
        Assert.Empty(ProbabilityDictionary.GetWords("а что и как в том же", IsStem: true));
    }

    // ----------------------------- Что НЕ должно попасть в список ------------

    [Theory]
    // Модальные: в договорах и регламентах различают смысл («должен уплатить»).
    [InlineData("должен")]
    [InlineData("обязан")]
    [InlineData("может")]
    [InlineData("вправе")]
    // Сравнительные: «не более 30 дней» против «не менее 30 дней».
    [InlineData("более")]
    [InlineData("менее")]
    [InlineData("ранее")]
    // Обычная лексика.
    [InlineData("договор")]
    [InlineData("срок")]
    [InlineData("оплата")]
    public void Meaningful_Words_Are_Not_Stopped(string word)
    {
        Assert.DoesNotContain(word, RussianStopWords.Default);
        Assert.Contains(word, ProbabilityDictionary.GetWords(word, IsStem: false));
    }

    // ----------------------------- Качество самого списка -------------------

    [Fact]
    public void Default_List_Has_No_Duplicates_Or_Blanks()
    {
        Assert.All(RussianStopWords.Default, w => Assert.False(string.IsNullOrWhiteSpace(w)));

        // Список написан руками — дубликат легко не заметить.
        var duplicates = RussianStopWords.Default
            .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, "Дубликаты в стоп-листе: " + string.Join(", ", duplicates));
    }

    [Fact]
    public void Default_List_Is_Lowercase_And_Nonempty()
    {
        Assert.NotEmpty(RussianStopWords.Default);
        Assert.All(RussianStopWords.Default, w => Assert.Equal(w.ToLowerInvariant(), w));
    }

    [Fact]
    public void Yo_And_Ye_Variants_Are_Both_Present()
    {
        // Нормализация «ё» → «е» не гарантирована на всех путях вызова токенизатора.
        Assert.Contains("еще", RussianStopWords.Default);
        Assert.Contains("ещё", RussianStopWords.Default);
        Assert.Contains("ее", RussianStopWords.Default);
        Assert.Contains("её", RussianStopWords.Default);
    }

    // ----------------------------- Подмена списка ---------------------------

    [Fact]
    public void Assigning_Stop_Array_Takes_Effect_Immediately()
    {
        // Обратная совместимость: поле публичное и подменяемое. Заодно проверяем,
        // что кэш-множество пересобирается при подмене ссылки, а не залипает.
        var original = ProbabilityDictionary.stop;
        try
        {
            ProbabilityDictionary.stop = new string[0];
            Assert.Contains("в", ProbabilityDictionary.GetWords("слово в тексте", IsStem: false));

            ProbabilityDictionary.stop = RussianStopWords.Default;
            Assert.DoesNotContain("в", ProbabilityDictionary.GetWords("слово в тексте", IsStem: false));

            ProbabilityDictionary.stop = new[] { "слово" };
            var words = ProbabilityDictionary.GetWords("слово в тексте", IsStem: false);
            Assert.DoesNotContain("слово", words);
            Assert.Contains("в", words);   // прежний список больше не действует
        }
        finally
        {
            ProbabilityDictionary.stop = original;
        }
    }

    // ----------------------------- Влияние на ранжирование ------------------

    [Fact]
    public void Ranking_Ignores_Service_Words()
    {
        var bm = new BM25(new[]
        {
            "Неустойка за просрочку платежа составляет 0,1 процента.",
            "В том же порядке и на тех же условиях, что и ранее.",
        });

        // Запрос из служебных слов не даёт терминов — совпадать нечему.
        Assert.All(bm.SearchTopN("и в том же что", 2), h => Assert.Equal(0, h.score));

        // Значимый запрос находит нужный документ.
        var hits = bm.SearchTopN("неустойка просрочка", 2);
        Assert.Equal(0, hits[0].index);
        Assert.True(hits[0].score > 0);
    }
}
