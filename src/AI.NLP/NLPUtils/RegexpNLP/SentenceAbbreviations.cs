using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AI.DataPrepaire.NLPUtils.RegexpNLP;

/// <summary>
/// Может ли точка после сокращения закончить предложение.
/// </summary>
/// <remarks>Нулевого значения нет намеренно: запись таблицы без класса не проходит проверку.</remarks>
public enum AbbreviationClass
{
    /// <summary>
    /// Сокращение-приставка: за ним всегда продолжение («см. раздел», «ст. 12», «т. е.»).
    /// </summary>
    NeverEnd = 1,

    /// <summary>
    /// Сокращение-хвост: может закончить предложение («5 000 руб.», «и т. д.»).
    /// </summary>
    MayEnd = 2,

    /// <summary>
    /// Хвост после числа, иначе приставка: «в 2026 г.», но «г. Тула»; «5 см.», но «см. рис. 3».
    /// </summary>
    MayEndAfterNumber = 3,
}

/// <summary>
/// Единственная таблица сокращений, которую знает разбивка на предложения.
/// </summary>
/// <remarks>
/// Ключ — сокращение в нижнем регистре без пробелов: «т.е.» находит и «т. е.», и «Т.Е.».
/// Однобуквенное сокращение с заглавной, которого нет в таблице, считается инициалом
/// (класс <see cref="AbbreviationClass.NeverEnd"/>) — это решает правило конца предложения.
/// </remarks>
public static class SentenceAbbreviations
{
    /// <summary>
    /// Записи таблицы: сокращение и его класс.
    /// </summary>
    public static IReadOnlyList<(string Abbreviation, AbbreviationClass Class)> Entries { get; } = new[]
    {
        // Приставки: ссылки, адреса, звания, вводные
        ("ср.", AbbreviationClass.NeverEnd), ("ст.", AbbreviationClass.NeverEnd), ("п.", AbbreviationClass.NeverEnd), ("пп.", AbbreviationClass.NeverEnd),
        ("гл.", AbbreviationClass.NeverEnd), ("разд.", AbbreviationClass.NeverEnd), ("прил.", AbbreviationClass.NeverEnd),
        ("рис.", AbbreviationClass.NeverEnd), ("табл.", AbbreviationClass.NeverEnd), ("напр.", AbbreviationClass.NeverEnd),
        ("т.е.", AbbreviationClass.NeverEnd), ("т.к.", AbbreviationClass.NeverEnd), ("т.ч.", AbbreviationClass.NeverEnd),
        ("т.н.", AbbreviationClass.NeverEnd), ("и.о.", AbbreviationClass.NeverEnd), ("им.", AbbreviationClass.NeverEnd),
        ("ок.", AbbreviationClass.NeverEnd), ("ул.", AbbreviationClass.NeverEnd), ("пр.", AbbreviationClass.NeverEnd),
        ("пер.", AbbreviationClass.NeverEnd), ("просп.", AbbreviationClass.NeverEnd), ("пл.", AbbreviationClass.NeverEnd),
        ("наб.", AbbreviationClass.NeverEnd), ("кв.", AbbreviationClass.NeverEnd), ("корп.", AbbreviationClass.NeverEnd),
        ("к.", AbbreviationClass.NeverEnd), ("о.", AbbreviationClass.NeverEnd), ("св.", AbbreviationClass.NeverEnd),
        ("проф.", AbbreviationClass.NeverEnd), ("акад.", AbbreviationClass.NeverEnd), ("доц.", AbbreviationClass.NeverEnd),
        ("гр.", AbbreviationClass.NeverEnd), ("куб.", AbbreviationClass.NeverEnd), ("англ.", AbbreviationClass.NeverEnd),
        ("лат.", AbbreviationClass.NeverEnd),

        // Хвосты: единицы, деньги, перечисления, даты
        ("т.д.", AbbreviationClass.MayEnd), ("т.п.", AbbreviationClass.MayEnd), ("тп.", AbbreviationClass.MayEnd),
        ("др.", AbbreviationClass.MayEnd), ("н.э.", AbbreviationClass.MayEnd), ("гг.", AbbreviationClass.MayEnd),
        ("вв.", AbbreviationClass.MayEnd), ("руб.", AbbreviationClass.MayEnd), ("коп.", AbbreviationClass.MayEnd),
        ("долл.", AbbreviationClass.MayEnd), ("тыс.", AbbreviationClass.MayEnd), ("млн.", AbbreviationClass.MayEnd),
        ("млрд.", AbbreviationClass.MayEnd), ("трлн.", AbbreviationClass.MayEnd), ("шт.", AbbreviationClass.MayEnd),
        ("экз.", AbbreviationClass.MayEnd), ("чел.", AbbreviationClass.MayEnd), ("мин.", AbbreviationClass.MayEnd),
        ("сек.", AbbreviationClass.MayEnd), ("дн.", AbbreviationClass.MayEnd), ("нед.", AbbreviationClass.MayEnd),
        ("мес.", AbbreviationClass.MayEnd),
        ("кг.", AbbreviationClass.MayEnd), ("км.", AbbreviationClass.MayEnd), ("мм.", AbbreviationClass.MayEnd),
        ("обл.", AbbreviationClass.MayEnd), ("янв.", AbbreviationClass.MayEnd), ("февр.", AbbreviationClass.MayEnd),
        ("апр.", AbbreviationClass.MayEnd), ("авг.", AbbreviationClass.MayEnd), ("сент.", AbbreviationClass.MayEnd),
        ("окт.", AbbreviationClass.MayEnd), ("нояб.", AbbreviationClass.MayEnd), ("дек.", AbbreviationClass.MayEnd),
        ("пн.", AbbreviationClass.MayEnd), ("вт.", AbbreviationClass.MayEnd), ("чт.", AbbreviationClass.MayEnd),
        ("пт.", AbbreviationClass.MayEnd), ("сб.", AbbreviationClass.MayEnd), ("вс.", AbbreviationClass.MayEnd),

        // Хвост после числа, приставка без него: год/город, рубль/река, час/часть, тонна/том, сантиметр/смотри
        ("г.", AbbreviationClass.MayEndAfterNumber), ("р.", AbbreviationClass.MayEndAfterNumber),
        ("в.", AbbreviationClass.MayEndAfterNumber), ("ч.", AbbreviationClass.MayEndAfterNumber),
        ("м.", AbbreviationClass.MayEndAfterNumber), ("т.", AbbreviationClass.MayEndAfterNumber),
        ("с.", AbbreviationClass.MayEndAfterNumber), ("л.", AbbreviationClass.MayEndAfterNumber),
        ("д.", AbbreviationClass.MayEndAfterNumber), ("стр.", AbbreviationClass.MayEndAfterNumber),
        ("см.", AbbreviationClass.MayEndAfterNumber),
    };

    private static readonly Dictionary<string, AbbreviationClass> s_classes =
        Entries.ToDictionary(e => e.Abbreviation, e => e.Class);

    // Длинные сокращения раньше коротких: «т. е.» не должно распасться на «т.» и «е.»
    private static readonly Regex s_pattern = new(
        @"(?<!\p{L})(?:" + string.Join("|", Entries
            .Select(e => e.Abbreviation)
            .OrderByDescending(a => a.Length)
            .Select(a => string.Join(@"\s?", a.TrimEnd('.').Split('.').Select(p => Regex.Escape(p) + @"\.")))) + ")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Вхождения сокращений таблицы в текст: начало, длина и класс.
    /// </summary>
    internal static IEnumerable<(int Start, int Length, AbbreviationClass Class)> Find(string text)
    {
        foreach (Match match in s_pattern.Matches(text))
            yield return (match.Index, match.Length, s_classes[Regex.Replace(match.Value, @"\s", "").ToLowerInvariant()]);
    }
}
