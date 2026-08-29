using AI.Script.Binding;
using AI.Script.Semantics;
using AI.Script.Runtime;
using System.Globalization;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>date</c>: даты и длительности.
/// </summary>
/// <remarks>
/// <see cref="Now"/> и <see cref="Today"/> — единственные функции стандартной библиотеки,
/// нарушающие воспроизводимость прогона. Обойтись без них нельзя, но опираться на них в
/// стадии конвейера не следует: результат перестанет совпадать между запусками.
/// </remarks>
[ScriptModule("date", "Даты и длительности: разбор, печать, части, арифметика", Version = "0.1")]
public static class DateModule
{
    [ScriptFn("now", "Текущий момент времени", Example = "date.now()")]
    public static DateTime Now() => DateTime.Now;

    [ScriptFn("today", "Сегодняшняя дата без времени", Example = "date.today()")]
    public static DateTime Today() => DateTime.Today;

    [ScriptFn("of", "Дата из чисел", Example = "date.of(2026, month: 8, day: 28)")]
    public static DateTime Of(
        [ScriptParam("год")] int year,
        [ScriptParam("месяц")] int month = 1,
        [ScriptParam("день")] int day = 1,
        [ScriptParam("час")] int hour = 0,
        [ScriptParam("минута")] int minute = 0,
        [ScriptParam("секунда")] int second = 0)
    {
        try
        {
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ScriptError(DiagnosticCodes.BadOperand, $"date.of: {exception.Message}");
        }
    }

    [ScriptFn("parse", "Разбирает дату из строки", Example = "date.parse(\"28.08.2026\", format: \"dd.MM.yyyy\")")]
    public static ScriptValue Parse(
        [ScriptParam("строка")] string text,
        [ScriptParam("формат; пусто — распознать автоматически")] string format = "")
    {
        bool ok = format.Length > 0
            ? DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value)
            : DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

        return ok ? ScriptValue.Date(value) : ScriptValue.None;
    }

    [ScriptFn("format", "Печатает дату по формату", Example = "date.format(d, format: \"yyyy-MM-dd\")")]
    public static string Format(
        [ScriptParam("дата")] DateTime d,
        [ScriptParam("формат .NET")] string format = "yyyy-MM-dd")
        => d.ToString(format, CultureInfo.InvariantCulture);

    [ScriptFn("year", "Год из даты", Example = "date.year(d)")]
    public static double Year([ScriptParam("дата")] DateTime d) => d.Year;

    [ScriptFn("month", "Номер месяца из даты, 1..12", Example = "date.month(d)")]
    public static double Month([ScriptParam("дата")] DateTime d) => d.Month;

    [ScriptFn("day", "День месяца", Example = "date.day(d)")]
    public static double Day([ScriptParam("дата")] DateTime d) => d.Day;

    [ScriptFn("hour", "Час из даты, 0..23", Example = "date.hour(d)")]
    public static double Hour([ScriptParam("дата")] DateTime d) => d.Hour;

    [ScriptFn("minute", "Минуты из даты, 0..59", Example = "date.minute(d)")]
    public static double Minute([ScriptParam("дата")] DateTime d) => d.Minute;

    [ScriptFn("weekday", "День недели: 1 — понедельник, 7 — воскресенье", Example = "date.weekday(d)")]
    public static double Weekday([ScriptParam("дата")] DateTime d) =>
        d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    [ScriptFn("add", "Прибавляет к дате указанные части", Example = "date.add(d, months: 1, days: -3)")]
    public static DateTime Add(
        [ScriptParam("дата")] DateTime d,
        [ScriptParam("лет")] int years = 0,
        [ScriptParam("месяцев")] int months = 0,
        [ScriptParam("дней")] int days = 0,
        [ScriptParam("часов")] int hours = 0,
        [ScriptParam("минут")] int minutes = 0)
        => d.AddYears(years).AddMonths(months).AddDays(days).AddHours(hours).AddMinutes(minutes);

    [ScriptFn("diff", "Разница между датами", Example = "date.diff(a, b)")]
    public static TimeSpan Diff(
        [ScriptParam("первая дата")] DateTime a,
        [ScriptParam("вторая дата")] DateTime b)
        => a - b;

    [ScriptFn("start_of", "Начало периода: day, month, year", Example = "date.start_of(d, unit: \"month\")")]
    public static DateTime StartOf(
        [ScriptParam("дата")] DateTime d,
        [ScriptParam("период: day, week, month, year")] string unit = "day")
        => unit switch
        {
            "day" => d.Date,
            "week" => d.Date.AddDays(-(d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1)),
            "month" => new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            "year" => new DateTime(d.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            _ => throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"date.start_of: неизвестный период '{unit}'",
                "поддержаны day, week, month, year"),
        };

    [ScriptFn("days", "Длительность в днях", Example = "date.days(dur)")]
    public static double Days([ScriptParam("длительность")] TimeSpan d) => d.TotalDays;

    [ScriptFn("hours", "Длительность в часах", Example = "date.hours(dur)")]
    public static double Hours([ScriptParam("длительность")] TimeSpan d) => d.TotalHours;

    [ScriptFn("seconds", "Длительность в секундах", Example = "date.seconds(dur)")]
    public static double Seconds([ScriptParam("длительность")] TimeSpan d) => d.TotalSeconds;
}
