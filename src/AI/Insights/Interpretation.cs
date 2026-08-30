using System.Collections.Generic;
using System.Text;

namespace AI.Insights;

/// <summary>Оценка метрики относительно предметных порогов.</summary>
public enum MetricQuality
{
    /// <summary>Порогов нет либо они неприменимы.</summary>
    Unknown,

    /// <summary>Значение в норме.</summary>
    Good,

    /// <summary>Нейтральное значение, само по себе ни хорошо, ни плохо.</summary>
    Neutral,

    /// <summary>Пограничное значение, требует внимания.</summary>
    Warning,

    /// <summary>Значение за пределами допустимого.</summary>
    Critical,
}

/// <summary>Одно число результата с единицей измерения, смыслом и оценкой.</summary>
/// <param name="Name">Название метрики.</param>
/// <param name="Value">Готовая строка значения.</param>
/// <param name="Unit">Единица измерения, если есть.</param>
/// <param name="Meaning">Что означает это число на практике.</param>
/// <param name="Quality">Оценка относительно порогов предметной области.</param>
public sealed record InterpretedMetric(
    string Name,
    string Value,
    string? Unit = null,
    string? Meaning = null,
    MetricQuality Quality = MetricQuality.Neutral);

/// <summary>
/// Разбор результата расчёта на естественном языке: что получилось, что из
/// этого следует, чему нельзя верить и что делать.
/// </summary>
/// <remarks>
/// <para>
/// Назначение — сделать вывод пригодным для чтения человеком и языковой
/// моделью без доступа к исходному коду. Голое число «эластичность −1,84»
/// ничего не сообщает тому, кто не знает ни знака, ни порогов, ни того, что
/// наивная регрессия на этих данных дала бы −0,42.
/// </para>
/// <para>
/// Разделение на четыре части не косметическое. <see cref="Findings"/> — то,
/// что следует из чисел; <see cref="Warnings"/> — нарушенные допущения,
/// из-за которых числам верить нельзя; <see cref="Recommendations"/> —
/// действия. Языковая модель, получившая такой блок, не обязана заново
/// выводить пороги и знаки: они уже проставлены тем кодом, который считал.
/// </para>
/// </remarks>
public sealed record Interpretation
{
    /// <summary>Что было посчитано.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Итог одним-двумя предложениями.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Ключевые числа с оценкой и пояснением.</summary>
    public IReadOnlyList<InterpretedMetric> Metrics { get; init; } = [];

    /// <summary>Выводы, следующие из чисел.</summary>
    public IReadOnlyList<string> Findings { get; init; } = [];

    /// <summary>Нарушенные допущения и границы применимости данного результата.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Что стоит сделать по итогам расчёта.</summary>
    public IReadOnlyList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Текстовое представление для передачи языковой модели или вывода
    /// в отчёт: заголовок, итог, метрики, выводы, предупреждения, рекомендации.
    /// </summary>
    /// <returns>Компактный структурированный текст.</returns>
    public string ToLlmText()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(Title)) sb.Append("### ").AppendLine(Title);
        if (!string.IsNullOrEmpty(Summary)) sb.AppendLine(Summary);

        if (Metrics.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Метрики:");
            foreach (InterpretedMetric m in Metrics)
            {
                sb.Append("- ").Append(m.Name).Append(" = ").Append(m.Value);
                if (!string.IsNullOrEmpty(m.Unit)) sb.Append(' ').Append(m.Unit);
                if (m.Quality != MetricQuality.Unknown) sb.Append(" [").Append(QualityWord(m.Quality)).Append(']');
                if (!string.IsNullOrEmpty(m.Meaning)) sb.Append(" — ").Append(m.Meaning);
                sb.AppendLine();
            }
        }

        AppendList(sb, "Выводы:", Findings);
        AppendList(sb, "Предупреждения:", Warnings);
        AppendList(sb, "Рекомендации:", Recommendations);

        return sb.ToString().TrimEnd();
    }

    /// <summary>Текстовое представление совпадает с <see cref="ToLlmText"/>.</summary>
    public override string ToString() => ToLlmText();

    private static void AppendList(StringBuilder sb, string header, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine(header);
        foreach (string item in items) sb.Append("- ").AppendLine(item);
    }

    private static string QualityWord(MetricQuality quality) => quality switch
    {
        MetricQuality.Good => "норма",
        MetricQuality.Warning => "внимание",
        MetricQuality.Critical => "проблема",
        MetricQuality.Neutral => "нейтрально",
        _ => "без оценки",
    };
}

/// <summary>
/// Результат расчёта, умеющий объяснить себя словами.
/// </summary>
/// <remarks>
/// Интерфейс реализуют результирующие типы доменных библиотек: вызов
/// <see cref="Interpret"/> не требует знать, какая именно модель отработала,
/// поэтому пайплайн «посчитать и объяснить» пишется один раз на все методы.
/// </remarks>
public interface IInterpretable
{
    /// <summary>Разбор результата на естественном языке.</summary>
    /// <returns>Итог, метрики, выводы, предупреждения и рекомендации.</returns>
    Interpretation Interpret();
}
