using AI.Insights;
using System.Linq;

namespace AI.Solvers.Chem.Metrology;

/// <summary>Разбор бюджета неопределённости по GUM.</summary>
public sealed partial class UncertaintyBudget : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret() => Interpret(0.95);

    /// <summary>Разбор бюджета для заданной доверительной вероятности.</summary>
    /// <param name="confidence">Доверительная вероятность</param>
    public Interpretation Interpret(double confidence)
    {
        double uc = CombinedStandardUncertainty;
        double expanded = ExpandedUncertainty(confidence);
        double relative = RelativeExpandedPercent(confidence);
        double veff = EffectiveDegreesOfFreedom;
        double k = CoverageFactor(confidence);

        UncertaintyComponent? dominant = Ranked.Count > 0 ? Ranked[0] : null;
        double dominantShare = dominant is null ? 0 : ContributionPercent(dominant);

        // Строки аргументов FindingIf вычисляются независимо от условия,
        // поэтому имя источника подготавливается заранее и не разыменовывает null
        string dominantName = dominant?.Name ?? string.Empty;
        int typeACount = Components.Count(c => c.IsTypeA);
        string unit = string.IsNullOrEmpty(Unit) ? string.Empty : " " + Unit;

        var builder = new InterpretationBuilder($"Бюджет неопределённости: {Measurand}")
            .Summary(Components.Count == 0
                ? $"Бюджет пуст: ни одной составляющей не задано, поэтому неопределённость результата "
                  + $"{Fmt.Num(Value, 4)}{unit} формально равна нулю."
                : $"Результат {Fmt.Num(Value, 4)} ± {Fmt.Num(expanded, 4)}{unit} "
                  + $"(P = {confidence:P0}, k = {Fmt.Num(k, 2)}). Суммарная стандартная неопределённость "
                  + $"{Fmt.Num(uc, 4)}{unit}, что составляет {Fmt.Num(relative, 1)} % от значения. "
                  + $"Составляющих {Components.Count}, из них типа A — {typeACount}.")
            .Metric("Значение", Fmt.Num(Value, 6), Unit, "оценка измеряемой величины")
            .Metric("u_c", Fmt.Num(uc, 6), Unit, "суммарная стандартная неопределённость")
            .Metric("U", Fmt.Num(expanded, 6), Unit, $"расширенная неопределённость при P = {confidence:P0}")
            .Metric("Относительная U", Fmt.Num(relative, 2), "%", "доля расширенной неопределённости от значения",
                double.IsNaN(relative) ? MetricQuality.Unknown
                    : relative <= 5 ? MetricQuality.Good
                    : relative <= 20 ? MetricQuality.Neutral
                    : MetricQuality.Warning)
            .Metric("k", Fmt.Num(k, 3), null, "коэффициент охвата из распределения Стьюдента")
            .Metric("ν_eff", double.IsInfinity(veff) ? "∞" : Fmt.Num(veff, 1), null,
                "эффективное число степеней свободы по Уэлчу — Саттертуэйту",
                double.IsInfinity(veff) || veff >= 30 ? MetricQuality.Good
                    : veff >= 10 ? MetricQuality.Neutral
                    : MetricQuality.Warning);

        if (dominant is not null)
        {
            builder = builder.Metric("Главный источник", dominant.Name, null,
                $"даёт {Fmt.Num(dominantShare, 1)} % дисперсии результата",
                dominantShare > 80 ? MetricQuality.Warning : MetricQuality.Neutral);
        }

        return builder
            .FindingIf(dominant is not null && dominantShare > 50,
                $"Бюджет определяется одним источником: «{dominantName}» даёт {Fmt.Num(dominantShare, 1)} % "
                + "дисперсии. Уменьшение остальных составляющих почти не изменит результат — работать нужно с ним.")
            .FindingIf(dominant is not null && dominantShare <= 50 && Components.Count > 1,
                "Вклады сопоставимы: ни один источник не доминирует, поэтому заметное улучшение требует "
                + "одновременного снижения нескольких составляющих.")
            .FindingIf(!double.IsInfinity(veff) && veff < 10,
                $"Эффективное число степеней свободы мало (ν_eff = {Fmt.Num(veff, 1)}), поэтому коэффициент "
                + $"охвата k = {Fmt.Num(k, 2)} заметно больше привычной двойки. Подстановка k = 2 занизила бы "
                + "интервал.")
            .FindingIf(typeACount == 0 && Components.Count > 0,
                "Все составляющие — типа B, то есть взяты из паспортов, допусков и справочных данных. "
                + "Ни одна не подтверждена серией параллельных измерений.")
            .Warning("Составляющие считаются некоррелированными: суммарная неопределённость получена как "
                + "корень из суммы квадратов вкладов. При наличии общего источника — одного прибора, "
                + "одной калибровки, одного оператора — оценка занижена.")
            .Warning("Коэффициенты чувствительности задаются вручную. Ошибка в частной производной "
                + "переносится в результат целиком и этим расчётом не обнаруживается.")
            .WarningIf(Components.Count == 0,
                "В бюджете нет ни одной составляющей: результат без неопределённости — не результат измерения.")
            .WarningIf(relative > 30,
                $"Относительная неопределённость {Fmt.Num(relative, 1)} % велика: результат годится для оценки "
                + "порядка величины, но не для сравнения с нормативом или с другим измерением.")
            .RecommendationIf(dominant is not null && dominantShare > 50,
                $"Сосредоточиться на источнике «{dominantName}»: именно он определяет достижимую точность.")
            .RecommendationIf(typeACount == 0 && Components.Count > 0,
                "Провести серию параллельных измерений: оценка типа A покажет, согласуется ли реальный "
                + "разброс с заявленными в паспортах допусками.")
            .Recommendation("Указывать в протоколе значение, U, коэффициент охвата и доверительную "
                + "вероятность вместе: без k интервал не интерпретируем.")
            .Build();
    }
}
