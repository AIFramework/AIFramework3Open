using System;
using System.Collections.Generic;

namespace AI.NLP.Morphology;

/// <summary>
/// Словарь закрытых классов русского языка: местоимения, числительные, предлоги, союзы,
/// частицы, частотные наречия, вспомогательные и неправильные глаголы.
/// </summary>
/// <remarks>
/// <para>
/// Закрытый класс — это класс, в который не приходят новые слова: предлогов в языке
/// столько же, сколько было сто лет назад. Такие слова можно просто перечислить,
/// и перечисление будет полным — в отличие от существительных и глаголов, где остаётся
/// только угадывать по суффиксу.
/// </para>
/// <para>
/// Словарь один на весь модуль: определитель части речи и лемматизатор читают его вместе.
/// Держать две таблицы — значит однажды поправить одну и получить разный разбор одного
/// слова в соседних вызовах, причём без единой ошибки сборки.
/// </para>
/// <para>
/// Ключи и значения нормализованы так же, как вход разбора (нижний регистр, ё → е).
/// Без этого формы с «ё» («моё», «своё», «трёх») были бы недостижимы: поиск идёт
/// по уже приведённому слову.
/// </para>
/// </remarks>
public static class RussianClosedClassLexicon
{
    private static readonly Dictionary<string, MorphAnalysis> Entries = Build();

    /// <summary>Число словоформ в словаре</summary>
    public static int Count => Entries.Count;

    /// <summary>Ищет разбор словоформы; вход нормализуется</summary>
    /// <param name="word">Словоформа</param>
    /// <param name="analysis">Найденный разбор</param>
    public static bool TryLookup(string word, out MorphAnalysis analysis)
        => TryLookupNormalized(RussianPhonetics.Normalize(word ?? string.Empty), out analysis);

    /// <summary>
    /// Ищет разбор уже нормализованной словоформы
    /// </summary>
    /// <param name="normalized">Словоформа после <see cref="RussianPhonetics.Normalize"/></param>
    /// <param name="analysis">Найденный разбор</param>
    public static bool TryLookupNormalized(string normalized, out MorphAnalysis analysis)
        => Entries.TryGetValue(normalized, out analysis);

    /// <summary>Ищет лемму уже нормализованной словоформы</summary>
    /// <param name="normalized">Словоформа после <see cref="RussianPhonetics.Normalize"/></param>
    /// <param name="lemma">Найденная лемма</param>
    public static bool TryGetLemmaNormalized(string normalized, out string lemma)
    {
        if (Entries.TryGetValue(normalized, out MorphAnalysis analysis))
        {
            lemma = analysis.Lemma;
            return true;
        }

        lemma = null;
        return false;
    }

    private static Dictionary<string, MorphAnalysis> Build()
    {
        var d = new Dictionary<string, MorphAnalysis>(StringComparer.Ordinal);

        // Порядок значим: при совпадении форм выигрывает запись, добавленная позже.
        // Пометки о таких столкновениях стоят рядом с соответствующими строками.
        void Add(PartOfSpeech pos, string lemma, params string[] forms)
        {
            string canonical = RussianPhonetics.Normalize(lemma);
            d[canonical] = new MorphAnalysis(canonical, pos);

            foreach (string f in forms)
                d[RussianPhonetics.Normalize(f)] = new MorphAnalysis(canonical, pos);
        }

        // ---- Местоимения ----
        Add(PartOfSpeech.Pronoun, "я", "меня", "мне", "мной", "мною");
        Add(PartOfSpeech.Pronoun, "ты", "тебя", "тебе", "тобой", "тобою");
        Add(PartOfSpeech.Pronoun, "он", "его", "него", "ему", "нему", "им", "ним", "нём", "нем");
        Add(PartOfSpeech.Pronoun, "она", "её", "ее", "неё", "нее", "ей", "ней", "ею", "нею");
        Add(PartOfSpeech.Pronoun, "оно");
        Add(PartOfSpeech.Pronoun, "мы", "нас", "нам", "нами");
        Add(PartOfSpeech.Pronoun, "вы", "вас", "вам", "вами");
        Add(PartOfSpeech.Pronoun, "они", "их", "них", "ими", "ними");
        Add(PartOfSpeech.Pronoun, "себя", "себе", "собой", "собою");

        Add(PartOfSpeech.Pronoun, "этот", "эта", "это", "эти", "этого", "этой", "этому", "этим", "этими", "этих", "этом", "эту");
        Add(PartOfSpeech.Pronoun, "тот", "та", "то", "те", "того", "той", "тому", "тем", "теми", "тех", "том", "ту");
        Add(PartOfSpeech.Pronoun, "такой", "такая", "такое", "такие", "такого", "такой", "такому", "таким", "такими", "таких", "таком", "такую");
        Add(PartOfSpeech.Pronoun, "весь", "вся", "всё", "все", "всего", "всей", "всему", "всем", "всеми", "всех", "всю");
        Add(PartOfSpeech.Pronoun, "каждый", "каждая", "каждое", "каждые", "каждого", "каждой", "каждому", "каждым", "каждыми", "каждых", "каждом", "каждую");
        // «сам» и «самый» делят формы «самого/самой/самому/самом»; ниже идёт «самый»,
        // и он выигрывает — как более частотный в письменном тексте.
        Add(PartOfSpeech.Pronoun, "сам", "сама", "само", "сами", "самим", "самими", "саму");
        Add(PartOfSpeech.Pronoun, "самый", "самая", "самое", "самые", "самого", "самой", "самому", "самым", "самыми", "самых", "самом", "самую");

        Add(PartOfSpeech.Pronoun, "кто", "кого", "кому", "кем", "ком");
        Add(PartOfSpeech.Pronoun, "что", "чего", "чему", "чем", "чём");
        Add(PartOfSpeech.Pronoun, "какой", "какая", "какое", "какие", "какого", "какой", "какому", "каким", "какими", "каких", "каком", "какую");
        Add(PartOfSpeech.Pronoun, "чей", "чья", "чьё", "чьи", "чьего", "чьей", "чьему", "чьим", "чьими", "чьих", "чьём", "чью");

        Add(PartOfSpeech.Pronoun, "мой", "моя", "моё", "мои", "моего", "моей", "моему", "моим", "моими", "моих", "моём", "мою");
        Add(PartOfSpeech.Pronoun, "твой", "твоя", "твоё", "твои", "твоего", "твоей", "твоему", "твоим", "твоими", "твоих", "твоём", "твою");
        Add(PartOfSpeech.Pronoun, "свой", "своя", "своё", "свои", "своего", "своей", "своему", "своим", "своими", "своих", "своём", "свою");
        Add(PartOfSpeech.Pronoun, "наш", "наша", "наше", "наши", "нашего", "нашей", "нашему", "нашим", "нашими", "наших", "нашем", "нашу");
        Add(PartOfSpeech.Pronoun, "ваш", "ваша", "ваше", "ваши", "вашего", "вашей", "вашему", "вашим", "вашими", "ваших", "вашем", "вашу");

        // ---- Числительные ----
        // Числительные — тоже закрытый класс, и склоняются они не по общим правилам:
        // «двух» никаким суффиксальным правилом в «два» не превращается.
        Add(PartOfSpeech.Numeral, "один", "одна", "одно", "одни", "одного", "одной", "одному", "одним", "одними", "одних", "одном", "одну");
        Add(PartOfSpeech.Numeral, "два", "две", "двух", "двум", "двумя");
        Add(PartOfSpeech.Numeral, "три", "трёх", "трём", "тремя");
        Add(PartOfSpeech.Numeral, "четыре", "четырёх", "четырём", "четырьмя");
        Add(PartOfSpeech.Numeral, "пять", "пяти", "пятью");
        Add(PartOfSpeech.Numeral, "шесть", "шести", "шестью");
        Add(PartOfSpeech.Numeral, "семь", "семи", "семью");
        Add(PartOfSpeech.Numeral, "восемь", "восьми", "восемью", "восьмью");
        Add(PartOfSpeech.Numeral, "девять", "девяти", "девятью");
        Add(PartOfSpeech.Numeral, "десять", "десяти", "десятью");
        Add(PartOfSpeech.Numeral, "сорок", "сорока");
        Add(PartOfSpeech.Numeral, "сто", "ста");
        Add(PartOfSpeech.Numeral, "оба", "обе", "обоих", "обеих", "обоим", "обеим");

        // ---- Вспомогательные и неправильные глаголы ----
        // «есть» омонимично (быть / принимать пищу); ниже стоит глагол еды, он и выигрывает.
        Add(PartOfSpeech.Verb, "быть", "был", "была", "было", "были",
                                       "буду", "будешь", "будет", "будем", "будете", "будут",
                                       "будь", "будьте", "суть");
        Add(PartOfSpeech.Verb, "иметь", "имею", "имеешь", "имеет", "имеем", "имеете", "имеют",
                                        "имел", "имела", "имело", "имели");
        Add(PartOfSpeech.Verb, "мочь", "могу", "можешь", "может", "можем", "можете", "могут",
                                       "мог", "могла", "могло", "могли");
        Add(PartOfSpeech.Verb, "хотеть", "хочу", "хочешь", "хочет", "хотим", "хотите", "хотят",
                                         "хотел", "хотела", "хотело", "хотели");
        Add(PartOfSpeech.Verb, "идти", "иду", "идёшь", "идёт", "идём",
                                       "идёте", "идут", "шёл", "шла", "шло", "шли",
                                       "иди", "идите");
        Add(PartOfSpeech.Verb, "ехать", "еду", "едешь", "едет", "едем", "едете", "едут",
                                        "ехал", "ехала", "ехало", "ехали");
        Add(PartOfSpeech.Verb, "дать", "дам", "дашь", "даст", "дадим", "дадите", "дадут",
                                       "дал", "дала", "дало", "дали", "дай", "дайте");
        Add(PartOfSpeech.Verb, "есть", "ем", "ешь", "ест", "едим", "едите", "едят",
                                       "ел", "ела", "ело", "ели");

        // ---- Существительные с супплетивным множественным ----
        // Список намеренно короткий: это не словарь, а перечень тех немногих слов,
        // где множественное число образовано от другого корня и правилу не поддаётся.
        Add(PartOfSpeech.Noun, "человек", "люди", "людей", "людям", "людьми", "людях");
        Add(PartOfSpeech.Noun, "ребёнок", "дети", "детей", "детям", "детьми", "детях");
        Add(PartOfSpeech.Noun, "год", "лет");

        // ---- Прилагательные с ударной флексией -ой (закрытый список) ----
        // Эти слова опасно ловить общими адъективными правилами: они дали бы -ий/-ый,
        // но лемма именно -ой (большой, а не «больший»).
        void AddAdj(string lemma)
        {
            string canonical = RussianPhonetics.Normalize(lemma);

            if (!canonical.EndsWith("ой", StringComparison.Ordinal))
            {
                // Контракт помощника: сюда попадают только леммы на «-ой». Молчаливый
                // пропуск прятал бы опечатку в списке ниже, поэтому падаем сразу.
                throw new ArgumentException(
                    $"AddAdj ожидает лемму на «-ой», получено «{lemma}».", nameof(lemma));
            }

            string stem = canonical.Substring(0, canonical.Length - 2);
            d[canonical] = new MorphAnalysis(canonical, PartOfSpeech.Adjective);

            // Флексии, общие для твёрдой и мягкой серий.
            string[] common = { "ого", "ому", "ом", "ая", "ой", "ую", "ою", "ое" };
            foreach (string f in common)
                d[stem + f] = new MorphAnalysis(canonical, PartOfSpeech.Adjective);

            // Серия зависит от последнего согласного основы: после шипящих и
            // заднеязычных — мягкая (большим, больших), иначе твёрдая (молодым, молодых).
            // Обе серии сразу засорили бы словарь несуществующими формами вроде «молодим».
            bool soft = stem.Length > 0 && RussianPhonetics.IsHushOrVelar(stem[stem.Length - 1]);
            string[] series = soft
                ? new[] { "им", "ие", "их", "ими" }
                : new[] { "ым", "ые", "ых", "ыми" };

            foreach (string f in series)
                d[stem + f] = new MorphAnalysis(canonical, PartOfSpeech.Adjective);
        }

        AddAdj("большой"); AddAdj("плохой"); AddAdj("молодой"); AddAdj("родной");
        AddAdj("простой"); AddAdj("прямой"); AddAdj("сырой"); AddAdj("крутой");
        AddAdj("скупой"); AddAdj("глухой"); AddAdj("немой"); AddAdj("святой");
        AddAdj("сухой"); AddAdj("тупой"); AddAdj("слепой"); AddAdj("живой");
        AddAdj("чужой"); AddAdj("пустой"); AddAdj("густой"); AddAdj("лихой");
        AddAdj("дорогой"); AddAdj("другой"); AddAdj("золотой");
        AddAdj("голубой"); AddAdj("седой"); AddAdj("гнедой"); AddAdj("холостой");

        // ---- Наречия ----
        // Наречие — открытый класс, но частотные наречия на -о совпадают по форме
        // со средним родом прилагательного и с существительным («тепло», «дело»),
        // поэтому самые обиходные перечислены: без этого общие правила дают
        // глагольный инфинитив («весело» → «весеть»).
        string[] adverbs =
        {
            "весело", "грустно", "быстро", "медленно", "громко", "тихо",
            "близко", "далеко", "рано", "поздно", "долго", "скоро",
            "хорошо", "плохо", "красиво", "удобно", "обычно", "внезапно",
            "интересно", "страшно", "приятно", "холодно", "тепло", "жарко",
            "больно", "смешно", "сложно", "просто", "легко", "тяжело",
            "мало", "много", "немного", "часто", "редко", "сильно", "слабо",
            "ясно", "точно", "вместе", "отдельно", "вдруг", "снова", "опять",
            "сегодня", "вчера", "завтра", "утром", "днём",
            "вечером", "ночью", "летом", "зимой", "весной", "осенью",
        };

        foreach (string a in adverbs)
        {
            string n = RussianPhonetics.Normalize(a);
            d[n] = new MorphAnalysis(n, PartOfSpeech.Adverb);
        }

        // Местоименные наречия и наречия следствия: «поэтому» без этой строки
        // разбирается адъективным правилом -ому и превращается в «поэтый».
        Add(PartOfSpeech.Adverb, "как"); Add(PartOfSpeech.Adverb, "так");
        Add(PartOfSpeech.Adverb, "там"); Add(PartOfSpeech.Adverb, "тут");
        Add(PartOfSpeech.Adverb, "здесь"); Add(PartOfSpeech.Adverb, "где");
        Add(PartOfSpeech.Adverb, "куда"); Add(PartOfSpeech.Adverb, "откуда");
        Add(PartOfSpeech.Adverb, "когда"); Add(PartOfSpeech.Adverb, "зачем");
        Add(PartOfSpeech.Adverb, "почему"); Add(PartOfSpeech.Adverb, "поэтому");
        Add(PartOfSpeech.Adverb, "потому"); Add(PartOfSpeech.Adverb, "затем");
        Add(PartOfSpeech.Adverb, "уже"); Add(PartOfSpeech.Adverb, "ещё");
        Add(PartOfSpeech.Adverb, "очень"); Add(PartOfSpeech.Adverb, "совсем");
        Add(PartOfSpeech.Adverb, "всегда"); Add(PartOfSpeech.Adverb, "никогда");
        Add(PartOfSpeech.Adverb, "иногда"); Add(PartOfSpeech.Adverb, "сейчас");
        Add(PartOfSpeech.Adverb, "теперь"); Add(PartOfSpeech.Adverb, "везде");
        Add(PartOfSpeech.Adverb, "нигде"); Add(PartOfSpeech.Adverb, "домой");

        // ---- Частицы ----
        Add(PartOfSpeech.Particle, "не"); Add(PartOfSpeech.Particle, "ни");
        Add(PartOfSpeech.Particle, "же"); Add(PartOfSpeech.Particle, "ли");
        Add(PartOfSpeech.Particle, "бы"); Add(PartOfSpeech.Particle, "уж");
        Add(PartOfSpeech.Particle, "вот"); Add(PartOfSpeech.Particle, "вон");
        Add(PartOfSpeech.Particle, "лишь"); Add(PartOfSpeech.Particle, "только");
        Add(PartOfSpeech.Particle, "даже"); Add(PartOfSpeech.Particle, "ведь");
        Add(PartOfSpeech.Particle, "разве"); Add(PartOfSpeech.Particle, "неужели");
        Add(PartOfSpeech.Particle, "именно"); Add(PartOfSpeech.Particle, "да");
        Add(PartOfSpeech.Particle, "нет"); Add(PartOfSpeech.Particle, "тоже");
        Add(PartOfSpeech.Particle, "также");

        // ---- Союзы ----
        Add(PartOfSpeech.Conjunction, "и"); Add(PartOfSpeech.Conjunction, "а");
        Add(PartOfSpeech.Conjunction, "но"); Add(PartOfSpeech.Conjunction, "или");
        Add(PartOfSpeech.Conjunction, "либо"); Add(PartOfSpeech.Conjunction, "если");
        Add(PartOfSpeech.Conjunction, "чтобы"); Add(PartOfSpeech.Conjunction, "хотя");
        Add(PartOfSpeech.Conjunction, "зато"); Add(PartOfSpeech.Conjunction, "однако");
        Add(PartOfSpeech.Conjunction, "пока"); Add(PartOfSpeech.Conjunction, "будто");
        Add(PartOfSpeech.Conjunction, "словно"); Add(PartOfSpeech.Conjunction, "ибо");
        Add(PartOfSpeech.Conjunction, "поскольку"); Add(PartOfSpeech.Conjunction, "дабы");

        // ---- Предлоги ----
        Add(PartOfSpeech.Preposition, "в"); Add(PartOfSpeech.Preposition, "во");
        Add(PartOfSpeech.Preposition, "на"); Add(PartOfSpeech.Preposition, "с");
        Add(PartOfSpeech.Preposition, "со"); Add(PartOfSpeech.Preposition, "к");
        Add(PartOfSpeech.Preposition, "ко"); Add(PartOfSpeech.Preposition, "у");
        Add(PartOfSpeech.Preposition, "о"); Add(PartOfSpeech.Preposition, "об");
        Add(PartOfSpeech.Preposition, "обо"); Add(PartOfSpeech.Preposition, "от");
        Add(PartOfSpeech.Preposition, "ото"); Add(PartOfSpeech.Preposition, "из");
        Add(PartOfSpeech.Preposition, "изо"); Add(PartOfSpeech.Preposition, "до");
        Add(PartOfSpeech.Preposition, "для"); Add(PartOfSpeech.Preposition, "над");
        Add(PartOfSpeech.Preposition, "надо"); Add(PartOfSpeech.Preposition, "под");
        Add(PartOfSpeech.Preposition, "подо"); Add(PartOfSpeech.Preposition, "за");
        Add(PartOfSpeech.Preposition, "при"); Add(PartOfSpeech.Preposition, "по");
        Add(PartOfSpeech.Preposition, "через"); Add(PartOfSpeech.Preposition, "между");
        Add(PartOfSpeech.Preposition, "без"); Add(PartOfSpeech.Preposition, "про");
        Add(PartOfSpeech.Preposition, "ради"); Add(PartOfSpeech.Preposition, "сквозь");
        Add(PartOfSpeech.Preposition, "среди"); Add(PartOfSpeech.Preposition, "около");
        Add(PartOfSpeech.Preposition, "возле"); Add(PartOfSpeech.Preposition, "вокруг");
        Add(PartOfSpeech.Preposition, "после"); Add(PartOfSpeech.Preposition, "перед");
        Add(PartOfSpeech.Preposition, "кроме"); Add(PartOfSpeech.Preposition, "вместо");
        Add(PartOfSpeech.Preposition, "против"); Add(PartOfSpeech.Preposition, "вдоль");

        return d;
    }
}
