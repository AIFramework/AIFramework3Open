using System;
using System.Collections.Generic;

namespace AI.NLP.Morphology;

/// <summary>
/// Определитель части речи для русского слова: словарь закрытых классов плюс правила
/// по суффиксу для открытых.
/// </summary>
/// <remarks>
/// <para>
/// Разбор идёт по слову отдельно, без учёта соседей. Это сознательное ограничение:
/// омонимию вида «стали» (глагол или существительное) в одиночном слове разрешить
/// нельзя в принципе, и притворяться, что можно, было бы хуже, чем сказать об этом.
/// Разбор с учётом контекста требует размеченного корпуса предложений, которого здесь нет.
/// </para>
/// <para>
/// Там, где суффикс принадлежит сразу двум частям речи, выбор сделан по цене ошибки,
/// а не по частоте. Приняв существительное за глагол, лемматизатор превращает «кредит»
/// в «кредить» — несуществующее слово, которое ни с чем в тексте не совпадёт. Приняв
/// глагол за существительное, он чаще всего возвращает слово нетронутым: оно останется
/// узнаваемым. Поэтому спорные окончания («-ом», «-ем», «-ой», «-ет») отнесены
/// к существительному, а глагол опознаётся по более длинному суффиксу с тематической
/// гласной («-ает», «-ует», «-аем»), который выигрывает по длине.
/// </para>
/// <para>
/// Различение прилагательного и причастия на разбор слова в начальную форму не влияет:
/// оба разбираются одной таблицей <see cref="Lemmatization.RussianLemmatizer"/>.
/// Оно нужно только разметке.
/// </para>
/// </remarks>
public sealed class RussianPosTagger : IPosTagger
{
    // Суффиксы причастий. Ищутся в хвосте слова, а не как окончание: за суффиксом
    // всегда идёт адъективная флексия («читающего», «прочитанным»).
    private static readonly string[] ParticipleMarks = { "вш", "ющ", "ущ", "ящ", "ащ", "нн" };

    // Окончания, по которым узнаётся глагольная основа под возвратным «-ся».
    private static readonly string[] VerbBeforeReflexive =
    {
        "ть", "л", "ла", "ло", "ли", "т", "шь", "м", "те", "сь"
    };

    private static readonly Dictionary<string, PartOfSpeech> BySuffix = BuildSuffixRules();
    private static readonly int MaxSuffixLength = 7;

    /// <summary>Общий потокобезопасный экземпляр (определитель без состояния)</summary>
    public static readonly RussianPosTagger Instance = new RussianPosTagger();

    /// <inheritdoc />
    public PartOfSpeech Tag(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return PartOfSpeech.Unknown;

        string w = RussianPhonetics.Normalize(word.Trim());

        if (w.Length == 0)
            return PartOfSpeech.Unknown;

        if (IsNumber(w))
            return PartOfSpeech.Numeral;

        // 1. Закрытые классы перечислены полностью — им верим безоговорочно.
        if (RussianClosedClassLexicon.TryLookupNormalized(w, out MorphAnalysis analysis))
            return analysis.PartOfSpeech;

        // 2. Возвратный постфикс: «учился», «делается». Проверяем, что под ним
        //    действительно глагольная форма, иначе сюда попадут «карася» и «Вася».
        if (w.Length > 4 && (EndsWith(w, "ся") || EndsWith(w, "сь")))
        {
            string stem = w.Substring(0, w.Length - 2);

            foreach (string ending in VerbBeforeReflexive)
                if (EndsWith(stem, ending))
                    return PartOfSpeech.Verb;
        }

        // 3. Причастие: суффикс стоит не в конце слова, поэтому ищется в хвосте.
        if (LooksLikeParticiple(w))
            return PartOfSpeech.Verb;

        // 4. Таблица суффиксов: выигрывает самый длинный подходящий.
        int start = Math.Min(MaxSuffixLength, w.Length - 1);

        for (int len = start; len >= 1; len--)
        {
            if (!BySuffix.TryGetValue(w.Substring(w.Length - len), out PartOfSpeech pos))
                continue;

            // У прилагательного основа не короче двух букв («злой», «синий»).
            // Без этой проверки «дому» разбирается как прилагательное на «-ому»
            // и остаётся неразобранным, хотя это дательный падеж от «дом».
            if (pos == PartOfSpeech.Adjective && w.Length - len < 2)
                continue;

            return pos;
        }

        // 5. Существительное — самый большой открытый класс, поэтому оно и по умолчанию.
        return PartOfSpeech.Noun;
    }

    /// <inheritdoc />
    public IReadOnlyList<PartOfSpeech> Tag(IReadOnlyList<string> words)
    {
        if (words == null) throw new ArgumentNullException(nameof(words));

        var tags = new PartOfSpeech[words.Count];

        for (int i = 0; i < words.Count; i++)
            tags[i] = Tag(words[i]);

        return tags;
    }

    private static bool EndsWith(string word, string suffix)
        => word.EndsWith(suffix, StringComparison.Ordinal);

    private static bool IsNumber(string w)
    {
        for (int i = 0; i < w.Length; i++)
            if (!char.IsDigit(w[i]) && w[i] != '.' && w[i] != ',' && w[i] != '-')
                return false;

        return char.IsDigit(w[0]) || (w.Length > 1 && char.IsDigit(w[1]));
    }

    private static bool LooksLikeParticiple(string w)
    {
        // Короткое слово причастием не бывает: «вши» и «нн» в нём — часть корня («вши», «сонный»)
        if (w.Length < 7) return false;

        // Суффикс причастия стоит перед флексией, то есть в хвосте, но не в самом конце
        string tail = w.Substring(w.Length - 6, 4);

        foreach (string mark in ParticipleMarks)
            if (tail.Contains(mark))
                return true;

        return false;
    }

    private static Dictionary<string, PartOfSpeech> BuildSuffixRules()
    {
        var rules = new Dictionary<string, PartOfSpeech>(StringComparer.Ordinal);

        void Add(PartOfSpeech pos, params string[] suffixes)
        {
            foreach (string s in suffixes)
                rules[s] = pos;
        }

        // ---- Существительные по словообразовательному суффиксу ----
        // Эти суффиксы длиннее адъективных флексий и потому выигрывают по длине:
        // «решение» разбирается как существительное, а не как прилагательное на «-ие».
        Add(PartOfSpeech.Noun,
            "ение", "ения", "ению", "ением", "ении", "ений", "ениям", "ениями", "ениях");
        Add(PartOfSpeech.Noun,
            "ание", "ания", "анию", "анием", "ании", "аний", "аниям", "аниями", "аниях");
        Add(PartOfSpeech.Noun,
            "ация", "ации", "ацию", "ацией", "аций", "ациям", "ациями", "ациях");
        Add(PartOfSpeech.Noun,
            "ость", "ости", "остью", "остей", "остям", "остями", "остях");
        Add(PartOfSpeech.Noun,
            "ство", "ства", "ству", "ством", "стве", "ствам", "ствами", "ствах");
        Add(PartOfSpeech.Noun,
            "тель", "теля", "телю", "телем", "теле", "тели", "телей", "телям", "телями", "телях");
        Add(PartOfSpeech.Noun,
            "ник", "ника", "нику", "ником", "нике", "ники", "ников", "никам", "никами", "никах");

        // ---- Глагол ----
        Add(PartOfSpeech.Verb, "ть", "ться", "тся", "чь");

        // Прошедшее время: только с тематической гласной. Голое «-л» отнесло бы
        // к глаголу «стол» и «угол».
        Add(PartOfSpeech.Verb,
            "ал", "ала", "ало", "али", "ял", "яла", "яло", "яли",
            "ил", "ила", "ило", "или", "ел", "ела", "ело", "ели",
            "ыл", "ыла", "ыло", "ыли", "ул", "ула", "уло", "ули");

        // Настоящее и будущее время с тематической гласной — длиннее одноимённых
        // именных окончаний («читаем» против «конем»), поэтому выигрывают.
        Add(PartOfSpeech.Verb,
            "аю", "аешь", "ает", "аем", "аете", "ают",
            "яю", "яешь", "яет", "яем", "яете", "яют",
            "ею", "еешь", "еет", "еем", "еете", "еют",
            "уешь", "ует", "уем", "уете", "уют",
            "ываю", "ывает", "ываем", "ывают", "иваю", "ивает", "иваем", "ивают");

        // Личные окончания без тематической гласной: «-ит» и «-ят» встречаются
        // у существительных заметно реже, чем у глаголов («говорит», «строят»).
        Add(PartOfSpeech.Verb, "ишь", "ите", "ит", "ят", "ат", "ешь", "ете");

        // Голое «-ю» намеренно оставлено существительному, хотя это и первое лицо
        // глагола («говорю»). Проверено на корпусе: дательный падеж «дню», «коню»
        // правило склонения разбирает верно, и отдать «-ю» глаголу значит потерять
        // больше, чем выиграть.

        // ---- Прилагательное ----
        Add(PartOfSpeech.Adjective,
            "ый", "ий", "ая", "яя", "ое", "ее", "ые", "ие",
            "ого", "его", "ому", "ему", "ым", "им", "ых", "их",
            "ую", "юю", "ыми", "ими", "ою", "ею");

        Add(PartOfSpeech.Adjective,
            "ейший", "ейшая", "ейшее", "ейшие", "ейшего", "ейшему",
            "ейшим", "ейших", "ейшими", "ейшую", "ейшем");

        // ---- Существительное по падежному окончанию ----
        // Отнесение спорных «-ом», «-ем», «-ой», «-ет» сюда объяснено в примечании
        // к классу: ошибка в эту сторону оставляет слово узнаваемым.
        Add(PartOfSpeech.Noun,
            "ом", "ем", "ов", "ев", "ам", "ям", "ами", "ями", "ах", "ях",
            "ой", "ей", "ью", "ья", "ье", "ьи", "ет", "ут", "ок", "ец", "изм", "ист");

        return rules;
    }
}
