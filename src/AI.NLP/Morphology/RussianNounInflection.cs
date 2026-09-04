using System;
using System.Collections.Generic;

namespace AI.NLP.Morphology;

/// <summary>
/// Приведение существительного к именительному падежу единственного числа
/// по падежному окончанию.
/// </summary>
/// <remarks>
/// <para>
/// По одной словоформе восстановить лемму существительного можно не всегда: «столам»
/// и «книгам» устроены одинаково, но первое — от «стол», второе — от «книга».
/// Окончание об этом не говорит ничего, и никакое количество правил здесь не поможет —
/// нужен словарь. Поэтому правила выбирают одно из двух разумных прочтений по
/// единому соглашению, а не пытаются угадывать каждый раз заново.
/// </para>
/// <para>
/// Соглашение — «нулевое окончание там, где оно возможно»: неоднозначное окончание
/// снимается, и получается основа («книгой» → «книг»). Женский род на «-а» при этом
/// теряется, зато все его формы сходятся к одному ключу, а мужской и средний род
/// разбираются верно. Обратное соглашение («книгой» → «книга») теряло бы мужской род
/// и вдобавок ломало бы устойчивость разбора: «стола» → «стола» разбиралось бы
/// в «стол» на втором проходе, то есть <c>Lemmatize(Lemmatize(x))</c> перестало бы
/// совпадать с <c>Lemmatize(x)</c>.
/// </para>
/// <para>
/// Два места, где правило не гадает, а знает:
/// беглая гласная («дня» → «день», а не «днь») и запрет на конец слова
/// вида «шумный + м/н» («окном» → «окно», а не «окн») — см.
/// <see cref="RussianPhonetics.CanEndWord"/>.
/// </para>
/// </remarks>
public static class RussianNounInflection
{
    // Продуктивные словообразовательные суффиксы. Их формы восстанавливаются точно:
    // «решением» → «решение» — здесь неоднозначности нет.
    private static readonly (string Ending, string Replacement)[] Derivational =
    {
        ("ениями", "ение"), ("ениям", "ение"), ("ениях", "ение"), ("ением", "ение"),
        ("ения", "ение"), ("ению", "ение"), ("ении", "ение"), ("ений", "ение"),
        ("аниями", "ание"), ("аниям", "ание"), ("аниях", "ание"), ("анием", "ание"),
        ("ания", "ание"), ("анию", "ание"), ("ании", "ание"), ("аний", "ание"),
        ("ациями", "ация"), ("ациям", "ация"), ("ациях", "ация"), ("ацией", "ация"),
        ("ации", "ация"), ("ацию", "ация"), ("аций", "ация"),
        ("остями", "ость"), ("остям", "ость"), ("остях", "ость"), ("остью", "ость"),
        ("остей", "ость"), ("ости", "ость"),
        ("ствами", "ство"), ("ствам", "ство"), ("ствах", "ство"), ("ством", "ство"),
        ("ства", "ство"), ("ству", "ство"), ("стве", "ство")
    };

    // Падежные окончания и способ восстановления начальной формы.
    // Порядок значим: выигрывает самое длинное подходящее окончание.
    private static readonly (string Ending, EndingKind Kind)[] Endings =
    {
        ("ями", EndingKind.Soft), ("ами", EndingKind.Hard),
        ("ям", EndingKind.Soft), ("ях", EndingKind.Soft),
        ("ам", EndingKind.Hard), ("ах", EndingKind.Hard),
        ("ов", EndingKind.GenitivePlural), ("ев", EndingKind.SoftOrJot),
        ("ей", EndingKind.Soft), ("ью", EndingKind.SoftSign),
        ("ой", EndingKind.Hard), ("ом", EndingKind.Hard),
        ("ем", EndingKind.SoftOrJot),
        ("а", EndingKind.Hard), ("ы", EndingKind.Hard), ("у", EndingKind.Hard),
        ("е", EndingKind.Hard), ("о", EndingKind.None),
        ("я", EndingKind.Soft), ("и", EndingKind.Soft), ("ю", EndingKind.Soft)
    };

    private enum EndingKind
    {
        /// <summary>Окончание не снимается</summary>
        None,

        /// <summary>Твёрдая основа: нулевое окончание либо средний род на «-о»</summary>
        Hard,

        /// <summary>Мягкая основа: нулевое окончание с мягким знаком</summary>
        Soft,

        /// <summary>Мягкая основа, но после гласной — «й» («музеем» → «музей»)</summary>
        SoftOrJot,

        /// <summary>Мягкий знак приписывается всегда («дверью» → «дверь»)</summary>
        SoftSign,

        /// <summary>Родительный падеж множественного числа на «-ов»</summary>
        GenitivePlural
    }

    /// <summary>
    /// Приводит существительное к именительному падежу единственного числа
    /// </summary>
    /// <param name="word">Словоформа; ожидается уже нормализованной</param>
    /// <returns>Начальная форма либо исходное слово, если правило не нашлось</returns>
    /// <remarks>
    /// Правила применяются до неподвижной точки, а не один раз. Иначе разбор не был бы
    /// устойчив: «системы» даёт основу «систем», которая сама оканчивается на «-ем»,
    /// и повторный вызов менял бы результат. Для поиска и индексации это важнее
    /// красоты отдельного разбора: все формы слова обязаны сходиться к одному ключу,
    /// сколько бы раз разбор ни повторили.
    /// </remarks>
    public static string ToNominative(string word)
    {
        if (word == null) return string.Empty;

        string current = word;

        // Ограничение на число проходов — защита от зацикливания на случай,
        // если правила когда-нибудь начнут возвращать друг друга.
        for (int pass = 0; pass < 4; pass++)
        {
            string next = ApplyOnce(current);

            if (next == current)
                break;

            current = next;
        }

        return current;
    }

    private static string ApplyOnce(string word)
    {
        if (word.Length < 3)
            return word;

        foreach ((string ending, string replacement) in Derivational)
        {
            if (word.Length > ending.Length + 1 && word.EndsWith(ending, StringComparison.Ordinal))
                return word.Substring(0, word.Length - ending.Length) + replacement;
        }

        foreach ((string ending, EndingKind kind) in Endings)
        {
            if (!word.EndsWith(ending, StringComparison.Ordinal))
                continue;

            string stem = word.Substring(0, word.Length - ending.Length);

            if (stem.Length < 2)
                continue;

            string lemma = Restore(stem, kind);

            if (lemma != null)
                return lemma;
        }

        return word;
    }

    private static string Restore(string stem, EndingKind kind)
    {
        char last = stem[stem.Length - 1];

        switch (kind)
        {
            case EndingKind.None:
                return null;

            case EndingKind.Hard:
                // Основа, оканчивающаяся на гласную, самостоятельным словом не бывает:
                // это «-ия», «-ея» и подобные, их разбор правилом только испортит.
                if (RussianPhonetics.IsVowel(last)) return null;

                return RussianPhonetics.CanEndWord(stem) ? stem : stem + "о";

            case EndingKind.GenitivePlural:
                // Слова, которые сами кончаются на «-ов» («ров», «плов»), защищены
                // требованием к длине основы.
                if (stem.Length < 3 || RussianPhonetics.IsVowel(last)) return null;

                return stem;

            case EndingKind.SoftSign:
                return last == 'ь' ? null : stem + "ь";

            case EndingKind.SoftOrJot:
                if (RussianPhonetics.IsVowel(last)) return stem + "й";

                return Soften(stem);

            case EndingKind.Soft:
                // «-ия», «-ья» и мягкий знак в основе означают, что слово уже
                // в начальной форме либо принадлежит парадигме, которую правило не берёт.
                if (RussianPhonetics.IsVowel(last) || last == 'ь') return null;

                return Soften(stem);

            default:
                return null;
        }
    }

    private static string Soften(string stem)
    {
        char last = stem[stem.Length - 1];

        // После «г», «к», «х» мягкого знака в русском не бывает: «книгь» невозможно.
        // Такая основа остаётся с нулевым окончанием.
        if (RussianPhonetics.IsHushOrVelar(last) && last != 'ж' && last != 'ш' && last != 'щ' && last != 'ч')
            return stem;

        if (RussianPhonetics.CanEndWord(stem))
            return stem + "ь";

        // Беглая гласная: «дн» словом быть не может, но «день» — может.
        return stem.Substring(0, stem.Length - 1) + "е" + last + "ь";
    }
}
