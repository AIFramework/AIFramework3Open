using System.Globalization;
using System.Text;

namespace AI.Script.Std;

/// <summary>
/// Разбор числа из текста, каким его пишут люди и выгружают учётные системы.
/// </summary>
/// <remarks>
/// Один разбор на всех: <c>dec.parse</c>, <c>table.clean</c> и чтение книг Excel обязаны
/// понимать «1 234,50 ₽», «(500)» и «12 %» одинаково, иначе одна и та же выгрузка читалась бы
/// по-разному в зависимости от того, через какую дверь она вошла.
/// <para>
/// Разделитель дробной части определяется по локали, а при конфликте — по последнему знаку:
/// «1.234,50» и «1,234.50» различаются только порядком, и угадывать тут нечего.
/// </para>
/// </remarks>
public static class DecimalText
{
    /// <summary>Знаки валют, которые снимаются с числа.</summary>
    private const string Currencies = "₽$€£¥₴₸";

    /// <summary>
    /// Слова, которые снимаются с числа: обозначения валют.
    /// </summary>
    /// <remarks>
    /// Только они, а не любые буквы: «SKU-100» и «Q1 2024» это коды и периоды, а «1E5» не сумма, и
    /// выбрасывание букв превращало их в -100, 12024 и 15.
    /// </remarks>
    private static readonly HashSet<string> s_currencyWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "руб", "р", "rub", "rur", "usd", "eur", "евро", "долл", "дол", "грн", "тг", "uah", "kzt", "cny", "gbp",
    };

    /// <summary>Разбирает число; <c>false</c>, если текст числом не является.</summary>
    /// <param name="text">Исходный текст ячейки.</param>
    /// <param name="locale">Локаль записи: <c>ru</c> либо <c>en</c>.</param>
    /// <param name="value">Разобранное число.</param>
    /// <param name="percent">Было ли число записано процентом.</param>
    public static bool TryParse(string? text, string locale, out decimal value, out bool percent)
    {
        value = 0;
        percent = false;

        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();
        bool negative = trimmed.StartsWith('(') && trimmed.EndsWith(')');

        if (negative) trimmed = trimmed[1..^1];

        var digits = new StringBuilder(trimmed.Length);
        var word = new StringBuilder();

        bool abbreviation = false;

        foreach (char c in trimmed)
        {
            if (char.IsLetter(c))
            {
                _ = word.Append(c);
                continue;
            }

            // Точка сразу за обозначением валюты это сокращение («руб.»), а не дробная часть.
            if (c == '.' && word.Length > 0)
            {
                if (!Currency(word)) return false;

                abbreviation = true;
                continue;
            }

            if (!Currency(word)) return false;

            if (abbreviation && c == '.') continue;

            abbreviation = false;

            if (char.IsDigit(c) || c is '.' or ',' or '-' or '+')
            {
                _ = digits.Append(c);
                continue;
            }

            if (c == '%') percent = true;

            // Пробелы любого вида (включая неразрывный) и знаки валют выбрасываются: они говорят
            // о единице измерения, а не о числе.
            if (char.IsWhiteSpace(c) || Currencies.Contains(c, StringComparison.Ordinal) || c == '%') continue;

            return false;
        }

        if (!Currency(word)) return false;

        string cleaned = digits.ToString();

        if (cleaned.Length == 0) return false;
        if (!TryParseCleaned(cleaned, locale, out value)) return false;

        if (negative) value = -value;
        if (percent) value /= 100m;

        return true;
    }

    /// <summary>Накопленное слово это обозначение валюты (либо слова нет); слово сбрасывается.</summary>
    private static bool Currency(StringBuilder word)
    {
        if (word.Length == 0) return true;

        bool known = s_currencyWords.Contains(word.ToString());

        _ = word.Clear();

        return known;
    }

    /// <summary>Разбирает число; отказ, если текст числом не является.</summary>
    public static decimal Parse(string? text, string locale, string what)
    {
        if (TryParse(text, locale, out decimal value, out _)) return value;

        throw new Runtime.ScriptError(
            Semantics.DiagnosticCodes.BadOperand,
            $"{what}: «{text}» не похоже на число",
            "ожидается запись вида «1 234,50», «1234.50 ₽», «12 %» либо «(500)» для отрицательного");
    }

    private static bool TryParseCleaned(string cleaned, string locale, out decimal value)
    {
        bool russian = !string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase);
        int comma = cleaned.LastIndexOf(',');
        int dot = cleaned.LastIndexOf('.');

        char fraction = comma >= 0 && dot >= 0
            ? (comma > dot ? ',' : '.')
            : comma >= 0 ? (russian ? ',' : Grouping(cleaned, ','))
            : dot >= 0 ? (russian ? Grouping(cleaned, '.') : '.')
            : '\0';

        string normalized = fraction == '\0'
            ? cleaned.Replace(",", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal)
            : Normalize(cleaned, fraction);

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Разделитель групп или дробной части, когда знак в тексте один.
    /// </summary>
    /// <remarks>
    /// «1,234» в русской записи — это одна целая двести тридцать четыре тысячных, а в
    /// английской — тысяча двести тридцать четыре. Спор решает ровно три цифры после знака:
    /// столько бывает в группе и почти не бывает в копейках.
    /// </remarks>
    private static char Grouping(string cleaned, char separator)
    {
        int position = cleaned.LastIndexOf(separator);

        return cleaned.Length - position - 1 == 3 ? '\0' : separator;
    }

    private static string Normalize(string cleaned, char fraction)
    {
        var builder = new StringBuilder(cleaned.Length);

        foreach (char c in cleaned)
        {
            if (c == fraction) _ = builder.Append('.');
            else if (c is not (',' or '.')) _ = builder.Append(c);
        }

        return builder.ToString();
    }
}
