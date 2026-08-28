using System;
using System.Collections.Generic;
using System.Globalization;

namespace AI.Economics.Insights;

/// <summary>
/// Построитель <see cref="Interpretation"/> с цепочкой вызовов.
/// </summary>
/// <remarks>
/// Условные добавления (<c>FindingIf</c>, <c>WarningIf</c>) существуют затем,
/// чтобы код интерпретации читался как список правил предметной области,
/// а не как лестница из <c>if</c>: каждое предупреждение стоит рядом с
/// условием, при котором оно уместно.
/// </remarks>
public sealed class InterpretationBuilder
{
    private readonly List<InterpretedMetric> _metrics = [];
    private readonly List<string> _findings = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _recommendations = [];
    private string _title = string.Empty;
    private string _summary = string.Empty;

    /// <summary>Создаёт построитель.</summary>
    /// <param name="title">Что было посчитано.</param>
    public InterpretationBuilder(string title = "") => _title = title;

    /// <summary>Задаёт заголовок.</summary>
    /// <param name="title">Что было посчитано.</param>
    public InterpretationBuilder Title(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>Задаёт итог одним-двумя предложениями.</summary>
    /// <param name="summary">Текст итога.</param>
    public InterpretationBuilder Summary(string summary)
    {
        _summary = summary;
        return this;
    }

    /// <summary>Добавляет метрику с готовой строкой значения.</summary>
    /// <param name="name">Название.</param>
    /// <param name="value">Значение строкой.</param>
    /// <param name="unit">Единица измерения.</param>
    /// <param name="meaning">Что означает это число.</param>
    /// <param name="quality">Оценка относительно порогов.</param>
    public InterpretationBuilder Metric(string name, string value, string? unit = null,
        string? meaning = null, MetricQuality quality = MetricQuality.Neutral)
    {
        _metrics.Add(new InterpretedMetric(name, value, unit, meaning, quality));
        return this;
    }

    /// <summary>Добавляет числовую метрику с инвариантным форматированием.</summary>
    /// <param name="name">Название.</param>
    /// <param name="value">Значение.</param>
    /// <param name="unit">Единица измерения.</param>
    /// <param name="meaning">Что означает это число.</param>
    /// <param name="quality">Оценка относительно порогов.</param>
    /// <param name="digits">Число знаков после запятой.</param>
    public InterpretationBuilder Metric(string name, double value, string? unit = null,
        string? meaning = null, MetricQuality quality = MetricQuality.Neutral, int digits = 2)
    {
        return Metric(name, Fmt.Num(value, digits), unit, meaning, quality);
    }

    /// <summary>Добавляет вывод, следующий из чисел.</summary>
    /// <param name="text">Текст вывода.</param>
    public InterpretationBuilder Finding(string text)
    {
        _findings.Add(text);
        return this;
    }

    /// <summary>Добавляет вывод, если условие выполнено.</summary>
    /// <param name="condition">Условие.</param>
    /// <param name="text">Текст вывода.</param>
    public InterpretationBuilder FindingIf(bool condition, string text) =>
        condition ? Finding(text) : this;

    /// <summary>Добавляет предупреждение о нарушенном допущении.</summary>
    /// <param name="text">Текст предупреждения.</param>
    public InterpretationBuilder Warning(string text)
    {
        _warnings.Add(text);
        return this;
    }

    /// <summary>Добавляет предупреждение, если условие выполнено.</summary>
    /// <param name="condition">Условие.</param>
    /// <param name="text">Текст предупреждения.</param>
    public InterpretationBuilder WarningIf(bool condition, string text) =>
        condition ? Warning(text) : this;

    /// <summary>Добавляет рекомендацию к действию.</summary>
    /// <param name="text">Текст рекомендации.</param>
    public InterpretationBuilder Recommendation(string text)
    {
        _recommendations.Add(text);
        return this;
    }

    /// <summary>Добавляет рекомендацию, если условие выполнено.</summary>
    /// <param name="condition">Условие.</param>
    /// <param name="text">Текст рекомендации.</param>
    public InterpretationBuilder RecommendationIf(bool condition, string text) =>
        condition ? Recommendation(text) : this;

    /// <summary>Собирает готовую интерпретацию.</summary>
    public Interpretation Build() => new()
    {
        Title = _title,
        Summary = _summary,
        Metrics = _metrics,
        Findings = _findings,
        Warnings = _warnings,
        Recommendations = _recommendations,
    };
}

/// <summary>
/// Форматирование чисел для интерпретаций.
/// </summary>
/// <remarks>
/// Культура инвариантная: текст интерпретации попадает в логи, отчёты и
/// промпты, и он не должен меняться от настроек локали сервера.
/// </remarks>
public static class Fmt
{
    /// <summary>Число с фиксированной точностью; бесконечность и NaN — словами.</summary>
    /// <param name="value">Значение.</param>
    /// <param name="digits">Знаков после запятой.</param>
    public static string Num(double value, int digits = 2)
    {
        if (double.IsNaN(value)) return "не определено";
        if (double.IsPositiveInfinity(value)) return "бесконечность";
        if (double.IsNegativeInfinity(value)) return "минус бесконечность";
        return value.ToString("F" + digits, CultureInfo.InvariantCulture);
    }

    /// <summary>Доля в процентах.</summary>
    /// <param name="value">Доля, где 1 соответствует 100 %.</param>
    /// <param name="digits">Знаков после запятой.</param>
    public static string Pct(double value, int digits = 1) =>
        double.IsNaN(value) ? "не определено" : Num(value * 100, digits) + " %";

    /// <summary>Денежная сумма в компактной записи.</summary>
    /// <param name="value">Сумма.</param>
    public static string Money(double value)
    {
        if (double.IsNaN(value)) return "не определено";

        double abs = Math.Abs(value);
        return abs switch
        {
            >= 1e9 => Num(value / 1e9) + " млрд",
            >= 1e6 => Num(value / 1e6) + " млн",
            >= 1e4 => Num(value / 1e3, 1) + " тыс.",
            _ => Num(value, 0),
        };
    }

    /// <summary>Целое число.</summary>
    /// <param name="value">Значение.</param>
    public static string Int(double value) =>
        double.IsNaN(value) ? "не определено"
        : double.IsInfinity(value) ? "бесконечность"
        : Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
}
