using System.Globalization;

namespace AI.Script.Office;

/// <summary>
/// Даты так, как их пишут в документах.
/// </summary>
/// <remarks>
/// В договоре дата встречается и цифрами («15.03.2026»), и словами («15 марта 2026 г.»), и в
/// машинном виде («2026-03-15»). Для извлечения это одна и та же дата, поэтому разбор здесь
/// один: иначе половина сроков не нашлась бы из-за того, что автор документа написал месяц
/// словом.
/// </remarks>
internal static class DocDates
{
    /// <summary>Форматы, которыми даты пишут цифрами.</summary>
    private static readonly string[] s_formats =
    [
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy",
        "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy",
    ];

    /// <summary>
    /// Месяцы словом: общее начало всех падежных форм.
    /// </summary>
    /// <remarks>
    /// Длинные начала стоят раньше коротких намеренно: «ма» — это май, но «март» начинается так
    /// же, и при обратном порядке март читался бы маем.
    /// </remarks>
    private static readonly (string Word, int Month)[] s_months =
    [
        ("январ", 1), ("феврал", 2), ("март", 3), ("апрел", 4), ("июн", 6), ("июл", 7),
        ("август", 8), ("сентябр", 9), ("октябр", 10), ("ноябр", 11), ("декабр", 12), ("ма", 5),
    ];

    /// <summary>Разбирает дату; <c>false</c> — текст датой не является.</summary>
    public static bool TryParse(string text, out DateTime value)
    {
        string trimmed = (text ?? string.Empty).Trim();

        if (DateTime.TryParseExact(trimmed, s_formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            return true;

        return TryWords(trimmed, out value);
    }

    /// <summary>Дата словами: «15 марта 2026».</summary>
    private static bool TryWords(string text, out DateTime value)
    {
        value = default;

        string[] parts = text.Split([' ', ' '], StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3) return false;

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int day)) return false;
        if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int year)) return false;

        int month = Month(parts[1]);

        if (month == 0 || day is < 1 or > 31) return false;

        try
        {
            value = new DateTime(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // «31 июня» — в документе опечатка, а не дата; молча подвинуть её на первое июля
            // значило бы выдать выдуманный срок за прочитанный.
            return false;
        }
    }

    /// <summary>Номер месяца по слову; 0 — слово не месяц.</summary>
    private static int Month(string word)
    {
        foreach ((string start, int month) in s_months)
        {
            if (word.StartsWith(start, StringComparison.OrdinalIgnoreCase)) return month;
        }

        return 0;
    }
}
