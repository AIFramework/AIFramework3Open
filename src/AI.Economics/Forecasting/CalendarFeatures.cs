using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;

namespace AI.Economics.Forecasting;

/// <summary>Набор календарных регрессоров с именами столбцов.</summary>
public sealed record CalendarMatrix
{
    /// <summary>Матрица признаков: строки — периоды, столбцы — регрессоры.</summary>
    public Matrix Features { get; init; } = new Matrix(0, 0);

    /// <summary>Имена столбцов в том же порядке.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>Даты, которым соответствуют строки.</summary>
    public IReadOnlyList<DateTime> Dates { get; init; } = [];
}

/// <summary>
/// Календарные регрессоры для моделей спроса: день недели, месяц, праздники,
/// гармоники Фурье.
/// </summary>
/// <remarks>
/// <para>
/// Календарь объясняет ту часть колебаний спроса, которую сезонная
/// составляющая модели описать не может: праздники сдвигаются по датам,
/// число рабочих дней в месяце различается, а перед длинными выходными
/// спрос на одни товары растёт, а на другие падает.
/// </para>
/// <para>
/// Гармоники Фурье предпочтительнее набора индикаторов, когда период длинный.
/// Для недельных данных с годовой сезонностью 52 индикатора съедят все
/// степени свободы, а две-три гармоники опишут ту же форму четырьмя
/// параметрами.
/// </para>
/// </remarks>
public static class CalendarFeatures
{
    /// <summary>Строит матрицу календарных признаков.</summary>
    /// <param name="start">Дата первого наблюдения.</param>
    /// <param name="periods">Число периодов.</param>
    /// <param name="step">Шаг между наблюдениями.</param>
    /// <param name="dayOfWeek">Включать индикаторы дней недели.</param>
    /// <param name="monthOfYear">Включать индикаторы месяцев.</param>
    /// <param name="fourierTerms">Число гармоник Фурье; 0 отключает.</param>
    /// <param name="fourierPeriod">Период гармоник в шагах ряда.</param>
    /// <param name="holidays">Даты праздников.</param>
    /// <param name="holidayWindow">
    /// Сколько периодов до и после праздника помечать: спрос обычно
    /// смещается, а не исчезает.
    /// </param>
    /// <returns>Матрица признаков с именами столбцов.</returns>
    /// <exception cref="ArgumentException">Некорректное число периодов.</exception>
    public static CalendarMatrix Build(
        DateTime start, int periods, TimeSpan step,
        bool dayOfWeek = false, bool monthOfYear = false,
        int fourierTerms = 0, int fourierPeriod = 52,
        IReadOnlyCollection<DateTime>? holidays = null, int holidayWindow = 0)
    {
        if (periods < 1) throw new ArgumentException("Число периодов должно быть положительным.", nameof(periods));

        var dates = new List<DateTime>(periods);
        for (int t = 0; t < periods; t++) dates.Add(start.Add(step * t));

        var columns = new List<double[]>();
        var names = new List<string>();

        if (dayOfWeek)
        {
            // Базовый уровень — понедельник, иначе матрица вырождена
            for (int d = 1; d < 7; d++)
            {
                int day = d;
                columns.Add([.. dates.Select(x => (int)x.DayOfWeek == day % 7 ? 1.0 : 0.0)]);
                names.Add($"dow_{DayName(day % 7)}");
            }
        }

        if (monthOfYear)
        {
            for (int m = 2; m <= 12; m++)
            {
                int month = m;
                columns.Add([.. dates.Select(x => x.Month == month ? 1.0 : 0.0)]);
                names.Add($"month_{month}");
            }
        }

        for (int k = 1; k <= fourierTerms; k++)
        {
            int harmonic = k;
            columns.Add([.. Enumerable.Range(0, periods)
                .Select(t => Math.Sin(2 * Math.PI * harmonic * t / fourierPeriod))]);
            names.Add($"sin_{harmonic}");

            columns.Add([.. Enumerable.Range(0, periods)
                .Select(t => Math.Cos(2 * Math.PI * harmonic * t / fourierPeriod))]);
            names.Add($"cos_{harmonic}");
        }

        if (holidays is { Count: > 0 })
        {
            var holidaySet = new HashSet<DateTime>(holidays.Select(h => h.Date));

            columns.Add([.. dates.Select(x => holidaySet.Contains(x.Date) ? 1.0 : 0.0)]);
            names.Add("holiday");

            for (int offset = 1; offset <= holidayWindow; offset++)
            {
                int shift = offset;
                columns.Add([.. dates.Select(x => holidaySet.Contains(x.Date.Add(step * shift)) ? 1.0 : 0.0)]);
                names.Add($"before_holiday_{shift}");

                columns.Add([.. dates.Select(x => holidaySet.Contains(x.Date.Add(step * -shift)) ? 1.0 : 0.0)]);
                names.Add($"after_holiday_{shift}");
            }
        }

        var matrix = new Matrix(periods, Math.Max(columns.Count, 1));
        for (int t = 0; t < periods; t++)
            for (int j = 0; j < columns.Count; j++) matrix[t, j] = columns[j][t];

        return new CalendarMatrix
        {
            Features = matrix,
            Names = names,
            Dates = dates,
        };
    }

    /// <summary>
    /// Праздничные даты России на заданные годы: фиксированные новогодние
    /// каникулы и прочие нерабочие дни без учёта переносов.
    /// </summary>
    /// <param name="fromYear">Первый год.</param>
    /// <param name="toYear">Последний год включительно.</param>
    /// <returns>Список дат.</returns>
    /// <remarks>
    /// Переносы выходных ежегодно утверждаются постановлением правительства
    /// и в этот список не входят: для точного календаря подставляйте
    /// собственный набор дат.
    /// </remarks>
    public static IReadOnlyList<DateTime> RussianHolidays(int fromYear, int toYear)
    {
        (int Month, int Day)[] fixedDays =
        [
            (1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (1, 6), (1, 7), (1, 8),
            (2, 23), (3, 8), (5, 1), (5, 9), (6, 12), (11, 4),
        ];

        var dates = new List<DateTime>();
        for (int year = fromYear; year <= toYear; year++)
            foreach ((int month, int day) in fixedDays) dates.Add(new DateTime(year, month, day));

        return dates;
    }

    private static string DayName(int dayOfWeek) => dayOfWeek switch
    {
        0 => "sun",
        1 => "mon",
        2 => "tue",
        3 => "wed",
        4 => "thu",
        5 => "fri",
        _ => "sat",
    };
}
