using System.Collections.Generic;

namespace AI.NLP.Morphology;

/// <summary>
/// Звуковые свойства русского слова, нужные разбору: гласные, шипящие и заднеязычные,
/// нормализация записи и проверка конца слова на произносимость.
/// </summary>
/// <remarks>
/// Класс собран затем, чтобы одни и те же наборы букв не заводились заново в теггере,
/// лемматизаторе и правилах склонения. Расхождение таких наборов между модулями
/// проявляется не ошибкой сборки, а разным разбором одного слова в разных местах.
/// </remarks>
public static class RussianPhonetics
{
    private static readonly HashSet<char> VowelSet =
        new HashSet<char>(new[] { 'а', 'е', 'ё', 'и', 'о', 'у', 'ы', 'э', 'ю', 'я' });

    // Шипящие и заднеязычные: после них в прилагательных мягкий вариант флексии (-ий),
    // а в существительных запрещены «ы» и «ю» (правило «жи-ши», «ча-ща», «чу-щу»).
    private static readonly HashSet<char> HushAndVelarSet =
        new HashSet<char>(new[] { 'г', 'к', 'х', 'ж', 'ш', 'щ', 'ч' });

    // Именно перед «м» и «н» в конце слова появляется беглая гласная
    // («окно» — «окон», «письмо» — «писем»), поэтому сочетание «шумный + м/н»
    // на конце слова практически не встречается.
    private static readonly HashSet<char> FillVowelTriggers =
        new HashSet<char>(new[] { 'м', 'н' });

    /// <summary>Гласная ли буква</summary>
    /// <param name="c">Буква</param>
    public static bool IsVowel(char c) => VowelSet.Contains(c);

    /// <summary>Шипящая или заднеязычная ли буква</summary>
    /// <param name="c">Буква</param>
    public static bool IsHushOrVelar(char c) => HushAndVelarSet.Contains(c);

    /// <summary>Есть ли в строке хотя бы одна гласная</summary>
    /// <param name="s">Строка</param>
    public static bool ContainsVowel(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;

        for (int i = 0; i < s.Length; i++)
            if (VowelSet.Contains(s[i])) return true;

        return false;
    }

    /// <summary>
    /// Каноническая запись слова: нижний регистр и ё → е
    /// </summary>
    /// <param name="s">Слово</param>
    /// <remarks>
    /// Именно <c>ToLowerInvariant</c>: <c>ToLower</c> зависит от текущей культуры
    /// (турецкая «I») и делал бы разбор зависящим от локали машины.
    /// </remarks>
    public static string Normalize(string s)
        => s == null ? string.Empty : s.ToLowerInvariant().Replace('ё', 'е');

    /// <summary>
    /// Может ли основа быть самостоятельным словом мужского рода с нулевым окончанием
    /// </summary>
    /// <param name="stem">Основа, полученная отсечением окончания</param>
    /// <remarks>
    /// <para>
    /// Проверка нужна там, где окончание не различает род. «Столом» и «окном» устроены
    /// одинаково, но «стол» — слово, а «окн» — нет: в конце русского слова сочетание
    /// «шумный + м/н» не произносится, и там, где оно возникает, появляется беглая
    /// гласная («окон», «писем»). Значит «окн» — основа среднего рода, и лемма «окно».
    /// </para>
    /// <para>
    /// Проверка односторонняя: она уверенно отбраковывает невозможные концовки,
    /// но не подтверждает, что слово существует. «Книг» она признаёт возможным,
    /// хотя лемма здесь «книга».
    /// </para>
    /// </remarks>
    public static bool CanEndWord(string stem)
    {
        if (string.IsNullOrEmpty(stem)) return false;

        char last = stem[stem.Length - 1];

        // Нулевое окончание мужского рода — это конец на согласную, «ь» или «й»
        if (IsVowel(last)) return false;

        if (stem.Length < 2) return true;

        char previous = stem[stem.Length - 2];

        if (!FillVowelTriggers.Contains(last))
            return true;

        // Перед «м»/«н» допустимы гласная, мягкий знак, «й», сонорные и другой носовой:
        // «дом», «сон», «фильм», «шторм», «гимн» — все возможны.
        return IsVowel(previous)
            || previous == 'ь' || previous == 'й'
            || previous == 'р' || previous == 'л'
            || FillVowelTriggers.Contains(previous);
    }
}
