using AI.NLP.Lemmatization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Тесты лемматизации русского языка. Закрывают исправленные баги
/// (ё в таблице исключений, недетерминизм разбора суффиксов, мусорные
/// адъективные формы, гонка в кэше) и фиксируют базовое поведение.
/// </summary>
public class LemmatizationTests
{
    private static readonly ILemmatizer Lemm = RussianLemmatizer.Instance;

    // ----------------------------- Нормализация «ё» -----------------------------

    [Theory]
    [InlineData("моё", "мой")]
    [InlineData("твоё", "твой")]
    [InlineData("своё", "свой")]
    [InlineData("чьё", "чей")]
    [InlineData("моём", "мой")]
    [InlineData("своём", "свой")]
    public void YoForms_ResolveThroughExceptions(string form, string expected)
    {
        // Регрессия: вход нормализовался («моё» -> «мое»), а ключи таблицы
        // исключений хранились с «ё» — поиск промахивался, и слово оставалось как есть.
        Assert.Equal(expected, Lemm.Lemmatize(form));
    }

    [Fact]
    public void YoAndYe_AreEquivalentInput()
    {
        Assert.Equal(Lemm.Lemmatize("ещё"), Lemm.Lemmatize("еще"));
        Assert.Equal(Lemm.Lemmatize("идёт"), Lemm.Lemmatize("идет"));
        Assert.Equal(Lemm.Lemmatize("всё"), Lemm.Lemmatize("все"));
    }

    // ----------------------------- Идемпотентность -----------------------------

    [Theory]
    [InlineData("красивого")]
    [InlineData("учился")]
    [InlineData("работали")]
    [InlineData("большим")]
    [InlineData("моё")]
    [InlineData("рисующий")]
    [InlineData("неизвестноеслово")]
    public void Lemmatize_IsIdempotent(string word)
    {
        string once = Lemm.Lemmatize(word);
        Assert.Equal(once, Lemm.Lemmatize(once));
    }

    // ----------------------------- Регистр и культура -----------------------------

    [Fact]
    public void Lemmatize_IsCaseInsensitive()
    {
        Assert.Equal("мой", Lemm.Lemmatize("МОЁ"));
        Assert.Equal("красивый", Lemm.Lemmatize("КрасИвого"));
    }

    [Fact]
    public void Lemmatize_DoesNotDependOnCurrentCulture()
    {
        // Регрессия: ToLower() зависел от культуры — в турецкой локали «I»
        // переходит в «ı», и латинские слова лемматизировались иначе.
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            string turkish = Lemm.Lemmatize("INDEX");

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string invariant = Lemm.Lemmatize("INDEX");

            Assert.Equal(invariant, turkish);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ----------------------------- Возвратные глаголы -----------------------------

    [Theory]
    [InlineData("учился", "учиться")]
    [InlineData("училась", "учиться")]
    [InlineData("учились", "учиться")]
    [InlineData("занимался", "заниматься")]
    [InlineData("делается", "делаться")]
    [InlineData("строится", "строиться")]
    public void ReflexiveVerbs_KeepSyaSuffix(string form, string expected)
    {
        // Возвратные суффиксы убраны из таблицы правил как недостижимые:
        // «-ся/-сь» снимается раньше, а основа разбирается обычными правилами.
        Assert.Equal(expected, Lemm.Lemmatize(form));
    }

    // ----------------------------- Прошедшее время -----------------------------

    [Theory]
    [InlineData("работали", "работать")]
    [InlineData("работал", "работать")]
    [InlineData("говорила", "говорить")]
    [InlineData("смотрели", "смотреть")]
    public void PastTense_MapsToInfinitive(string form, string expected)
        => Assert.Equal(expected, Lemm.Lemmatize(form));

    // ----------------------------- Прилагательные -----------------------------

    [Theory]
    [InlineData("красивого", "красивый")]
    [InlineData("красивому", "красивый")]
    [InlineData("красивыми", "красивый")]
    [InlineData("синего", "синий")]
    [InlineData("синим", "синий")]
    public void Adjectives_CollapseToNominative(string form, string expected)
        => Assert.Equal(expected, Lemm.Lemmatize(form));

    [Fact]
    public void AmbiguousSuffixes_ResolveAsAdjective_Deterministically()
    {
        // «-им» и «-ую» принадлежат двум частям речи. Раньше победитель зависел
        // от нестабильной сортировки правил; теперь выбор зафиксирован в пользу
        // прилагательного — он «схлопывает» целую падежную парадигму.
        Assert.Equal("синий", Lemm.Lemmatize("синим"));
        Assert.Equal("новый", Lemm.Lemmatize("новую"));

        // И этот выбор стабилен между вызовами и экземплярами.
        var another = RussianLemmatizer.Instance;
        Assert.Equal(Lemm.Lemmatize("синим"), another.Lemmatize("синим"));
    }

    [Theory]
    [InlineData("большим", "большой")]
    [InlineData("больших", "большой")]
    [InlineData("большими", "большой")]
    [InlineData("большую", "большой")]
    [InlineData("молодым", "молодой")]
    [InlineData("молодых", "молодой")]
    [InlineData("другими", "другой")]
    public void StressedOyAdjectives_KeepOyLemma(string form, string expected)
    {
        // Закрытый список прилагательных на «-ой»: общие правила дали бы «больший».
        Assert.Equal(expected, Lemm.Lemmatize(form));
    }

    [Fact]
    public void StressedOyAdjectives_DoNotGenerateWrongSeries()
    {
        // Регрессия: раньше добавлялись обе серии флексий сразу, и в словарь
        // попадали несуществующие формы («молодим», «большым»).
        Assert.NotEqual("молодой", Lemm.Lemmatize("молодим"));
        Assert.NotEqual("большой", Lemm.Lemmatize("большым"));
    }

    // ----------------------------- Причастия -----------------------------

    [Theory]
    [InlineData("рисующий", "рисовать")]
    [InlineData("рисующего", "рисовать")]
    [InlineData("читавший", "читать")]
    [InlineData("говорящий", "говорить")]
    public void Participles_MapToInfinitive(string form, string expected)
        => Assert.Equal(expected, Lemm.Lemmatize(form));

    // ----------------------------- Наречия и служебные -----------------------------

    [Theory]
    [InlineData("весело")]
    [InlineData("быстро")]
    [InlineData("хорошо")]
    [InlineData("сегодня")]
    public void Adverbs_StayUnchanged(string word)
    {
        // Без POS-тега общие правила превратили бы «весело» в «весеть».
        Assert.Equal(word, Lemm.Lemmatize(word));
    }

    // ----------------------------- Граничные случаи -----------------------------

    [Fact]
    public void EmptyAndNull_AreSafe()
    {
        Assert.Equal(string.Empty, Lemm.Lemmatize(null));
        Assert.Equal(string.Empty, Lemm.Lemmatize(string.Empty));
        Assert.Equal(string.Empty, Lemm.Lemmatize("   "));
    }

    [Fact]
    public void UnknownWord_ReturnedNormalizedNotMangled()
    {
        // Лемматизатор, в отличие от стеммера, не обязан резать незнакомое слово.
        Assert.Equal("бармаглот", Lemm.Lemmatize("Бармаглот"));
    }

    // ----------------------------- Обход текста -----------------------------

    [Fact]
    public void LemmatizeSentence_PreservesPunctuationAndSpacing()
    {
        string result = Lemm.LemmatizeSentence("Красивые дома, большие окна!");

        Assert.Contains(",", result);
        Assert.EndsWith("!", result);
        Assert.Contains("красивый", result);
    }

    [Fact]
    public void LemmatizeToWords_SkipsPunctuation()
    {
        string[] words = Lemm.LemmatizeToWords("Работали, работали — и всё.");

        Assert.Equal(4, words.Length);
        Assert.Equal("работать", words[0]);
        Assert.Equal("работать", words[1]);
    }

    [Fact]
    public void LemmatizeAll_HandlesNullCollection()
        => Assert.Empty(Lemm.LemmatizeAll(null));

    // ----------------------------- CachingLemmatizer -----------------------------

    [Fact]
    public void Caching_ReturnsSameResultsAsInner()
    {
        var cached = new CachingLemmatizer(RussianLemmatizer.Instance);
        string[] words = { "красивого", "учился", "моё", "работали", "синим" };

        foreach (string w in words)
        {
            Assert.Equal(RussianLemmatizer.Instance.Lemmatize(w), cached.Lemmatize(w));
            Assert.Equal(RussianLemmatizer.Instance.Lemmatize(w), cached.Lemmatize(w)); // из кэша
        }

        Assert.Equal(words.Length, cached.CacheSize);

        cached.ClearCache();
        Assert.Equal(0, cached.CacheSize);
    }

    [Fact]
    public void Caching_RespectsMaxSize()
    {
        var cached = new CachingLemmatizer(RussianLemmatizer.Instance, maxSize: 2);

        cached.Lemmatize("красивого");
        cached.Lemmatize("учился");
        cached.Lemmatize("работали");

        // Лимит мягкий (проверка и вставка не атомарны), но рост должен быть ограничен.
        Assert.True(cached.CacheSize <= 2, $"Размер кэша {cached.CacheSize} превысил лимит.");
    }

    [Fact]
    public void Caching_IsThreadSafe()
    {
        var cached = new CachingLemmatizer(RussianLemmatizer.Instance);
        string[] words = { "красивого", "учился", "работали", "синим", "рисующий" };
        var results = new string[8][];

        Parallel.For(0, 8, i =>
        {
            var local = new string[words.Length];
            for (int r = 0; r < 200; r++)
                for (int w = 0; w < words.Length; w++)
                    local[w] = cached.Lemmatize(words[w]);
            results[i] = local;
        });

        foreach (string[] r in results)
            Assert.Equal(results[0], r);
    }

    // ----------------------------- DictionaryLemmatizer -----------------------------

    [Fact]
    public void Dictionary_OverridesRulesAndFallsBack()
    {
        var dict = new Dictionary<string, string> { ["окна"] = "окно" };
        var lemm = new DictionaryLemmatizer(dict, RussianLemmatizer.Instance);

        Assert.Equal("окно", lemm.Lemmatize("окна"));      // из словаря
        Assert.Equal("окно", lemm.Lemmatize("Окна"));      // регистр не важен
        Assert.Equal("красивый", lemm.Lemmatize("красивого")); // через fallback
        Assert.Equal(1, lemm.Count);
    }

    [Fact]
    public void Dictionary_LoadFromReader_SkipsCommentsAndBlanks()
    {
        string content = string.Join("\n",
            "# комментарий",
            "",
            "окна\tокно",
            "домов\tдом",
            "строка_без_разделителя");

        using var reader = new StringReader(content);
        DictionaryLemmatizer lemm = DictionaryLemmatizer.LoadFromReader(reader);

        Assert.Equal(2, lemm.Count);
        Assert.Equal("окно", lemm.Lemmatize("окна"));
        Assert.Equal("дом", lemm.Lemmatize("домов"));
    }

    [Fact]
    public void Dictionary_NormalizesYoInKeys()
    {
        var dict = new Dictionary<string, string> { ["ёлки"] = "ёлка" };
        var lemm = new DictionaryLemmatizer(dict, IdentityLemmatizer.Instance);

        Assert.Equal("ёлка", lemm.Lemmatize("ёлки"));
        Assert.Equal("ёлка", lemm.Lemmatize("елки"));
    }

    // ----------------------------- Фабрика -----------------------------

    [Fact]
    public void Factory_CreateRussian_WorksWithAndWithoutCache()
    {
        Assert.Equal("красивый", Lemmatizer.CreateRussian(withCache: true).Lemmatize("красивого"));
        Assert.Equal("красивый", Lemmatizer.CreateRussian(withCache: false).Lemmatize("красивого"));
    }

    [Fact]
    public void Identity_ReturnsInput()
    {
        Assert.Equal("КрасИвого", IdentityLemmatizer.Instance.Lemmatize("КрасИвого"));
        Assert.Equal(string.Empty, IdentityLemmatizer.Instance.Lemmatize(null));
    }
}
