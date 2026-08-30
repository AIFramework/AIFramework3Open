using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Credit;

/// <summary>Настройки построения скоркарты.</summary>
public sealed record ScorecardOptions
{
    /// <summary>Минимальная информационная ценность для отбора признака.</summary>
    public double MinInformationValue { get; init; } = 0.02;

    /// <summary>
    /// Максимальная информационная ценность: признаки выше порога исключаются
    /// как подозрительные на утечку целевой переменной.
    /// </summary>
    public double MaxInformationValue { get; init; } = 0.8;

    /// <summary>Максимальное число интервалов на признак.</summary>
    public int MaxBins { get; init; } = 6;

    /// <summary>Минимальная доля наблюдений в интервале.</summary>
    public double MinBinShare { get; init; } = 0.05;

    /// <summary>Балл, соответствующий базовым шансам.</summary>
    public double BaseScore { get; init; } = 600;

    /// <summary>Базовое отношение шансов «исправный к дефолту» на базовом балле.</summary>
    public double BaseOdds { get; init; } = 50;

    /// <summary>Число баллов, удваивающее шансы.</summary>
    public double PointsToDoubleOdds { get; init; } = 20;

    /// <summary>Коэффициент гребневой регуляризации логистической регрессии.</summary>
    public double Ridge { get; init; } = 1e-3;
}

/// <summary>Строка скоркарты: сколько баллов даёт попадание в интервал.</summary>
/// <param name="Variable">Признак.</param>
/// <param name="Bin">Интервал.</param>
/// <param name="Woe">Вес доказательства интервала.</param>
/// <param name="Coefficient">Коэффициент признака в логистической регрессии.</param>
/// <param name="Points">Баллы, начисляемые за попадание в интервал.</param>
public sealed record ScorecardPoint(string Variable, string Bin, double Woe, double Coefficient, double Points);

/// <summary>Итог построения скоркарты.</summary>
public sealed record ScorecardResult : IInterpretable
{
    /// <summary>Биннинг отобранных признаков.</summary>
    public IReadOnlyList<VariableBinning> Variables { get; init; } = [];

    /// <summary>Строки скоркарты по признакам и интервалам.</summary>
    public IReadOnlyList<ScorecardPoint> Points { get; init; } = [];

    /// <summary>Признаки, отклонённые при отборе, с причиной.</summary>
    public IReadOnlyList<(string Variable, double InformationValue, string Reason)> Rejected { get; init; } = [];

    /// <summary>Свободный член логистической регрессии.</summary>
    public double Intercept { get; init; }

    /// <summary>Коэффициенты при весах доказательства.</summary>
    public IReadOnlyList<double> Coefficients { get; init; } = [];

    /// <summary>Базовые баллы, начисляемые каждому заявителю.</summary>
    public double BasePoints { get; init; }

    /// <summary>Качество модели на обучающей выборке.</summary>
    public ScoreQuality Quality { get; init; } = new();

    /// <summary>Баллы, соответствующие удвоению шансов.</summary>
    public double PointsToDoubleOdds { get; init; }

    /// <summary>Минимальный и максимальный достижимый балл.</summary>
    public (double Min, double Max) ScoreRange { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var strongest = Variables.OrderByDescending(v => v.InformationValue).FirstOrDefault();
        double span = ScoreRange.Max - ScoreRange.Min;
        int nonMonotone = Variables.Count(v => !v.IsMonotone);

        var builder = new InterpretationBuilder("Скоринговая карта")
            .Summary($"В модель отобрано {Variables.Count} признаков из " +
                     $"{Variables.Count + Rejected.Count}. Коэффициент Джини " +
                     $"{Fmt.Num(Quality.Gini, 3)}, шкала баллов от {Fmt.Num(ScoreRange.Min, 0)} " +
                     $"до {Fmt.Num(ScoreRange.Max, 0)}; каждые " +
                     $"{Fmt.Num(PointsToDoubleOdds, 0)} баллов удваивают шансы на возврат.")
            .Metric("Джини", Quality.Gini, null, "разделяющая способность на обучающей выборке",
                Quality.Gini > 0.4 ? MetricQuality.Good
                    : Quality.Gini > 0.25 ? MetricQuality.Warning : MetricQuality.Critical, 3)
            .Metric("Признаков в модели", Variables.Count, null,
                $"отклонено {Rejected.Count}", MetricQuality.Neutral, 0)
            .Metric("Диапазон баллов", $"{Fmt.Num(ScoreRange.Min, 0)}–{Fmt.Num(ScoreRange.Max, 0)}", null,
                $"размах {Fmt.Num(span, 0)} баллов")
            .Metric("Шаг удвоения шансов", PointsToDoubleOdds, "баллов",
                "стандартная шкала кредитного скоринга", MetricQuality.Neutral, 0)
            .Metric("Средний прогноз", Fmt.Pct(Quality.MeanPredicted, 2), null,
                $"фактически {Fmt.Pct(Quality.MeanObserved, 2)}");

        foreach (VariableBinning variable in Variables.OrderByDescending(v => v.InformationValue))
        {
            builder.Metric($"IV: {variable.Variable}", variable.InformationValue, null,
                variable.Predictiveness, MetricQuality.Unknown, 3);
        }

        return builder
            .FindingIf(strongest is not null,
                $"Наибольшую предсказательную силу даёт признак «{strongest?.Variable}» " +
                $"с информационной ценностью {Fmt.Num(strongest?.InformationValue ?? 0, 3)}.")
            .Finding("Баллы аддитивны по признакам, а шкала линейна в логарифме шансов. " +
                     "Отсюда главное практическое свойство скоркарты: решение можно объяснить " +
                     "заявителю и регулятору построчно, без ссылок на устройство модели.")
            .FindingIf(Rejected.Count > 0,
                $"Отклонено признаков: {Rejected.Count}. Основные причины — недостаточная " +
                "информационная ценность и подозрение на утечку целевой переменной.")
            .WarningIf(nonMonotone > 0,
                $"У {nonMonotone} признаков связь с риском немонотонна. Такие зависимости " +
                "часто оказываются шумом выборки и плохо переносятся на новые заявки.")
            .WarningIf(Coefficients.Any(c => c < 0),
                "Есть признаки с отрицательным коэффициентом при весе доказательства. " +
                "Это означает, что модель переворачивает смысл переменной — обычно признак " +
                "коллинеарности с другой.")
            .WarningIf(Quality.Defaults < 100,
                $"Дефолтов в обучающей выборке всего {Quality.Defaults}. Отраслевое правило " +
                "требует минимум несколько сотен для устойчивой скоркарты.")
            .Warning("Метрики посчитаны на обучающей выборке и потому оптимистичны. " +
                     "Перед внедрением проверьте карту на отложенной выборке и на более " +
                     "позднем периоде наблюдений.")
            .Recommendation("Зафиксируйте границы интервалов вместе с картой: пересчёт " +
                            "биннинга на новых данных меняет смысл баллов и делает " +
                            "исторические отсечки несопоставимыми.")
            .Recommendation("Настройте регулярный мониторинг индекса стабильности по итоговому " +
                            "баллу и по каждому признаку карты.")
            .Build();
    }
}

/// <summary>
/// Скоринговая карта: логистическая регрессия на весах доказательства
/// с переводом в целочисленную шкалу баллов.
/// </summary>
/// <remarks>
/// <para>
/// Построение идёт в четыре шага: биннинг признаков с расчётом веса
/// доказательства, отбор по информационной ценности, логистическая регрессия
/// на весах и перевод коэффициентов в баллы.
/// </para>
/// <para>
/// Перевод в баллы задаётся двумя числами — базовым баллом при известных
/// шансах и числом баллов, удваивающим шансы:
/// </para>
/// <code>
/// factor = PDO / ln(2)
/// offset = baseScore - factor * ln(baseOdds)
/// points_ij = -(beta_j * WoE_ij + alpha / k) * factor + offset / k
/// </code>
/// <para>
/// Смысл конструкции не в математике, а в объяснимости. Решение по заявке
/// раскладывается на вклад каждого признака построчно, и его можно предъявить
/// заявителю, кредитному комитету и регулятору без ссылок на устройство
/// модели. Именно поэтому скоркарты продолжают применяться там, где градиентный
/// бустинг дал бы больший коэффициент Джини.
/// </para>
/// </remarks>
public sealed class Scorecard
{
    private VariableBinning[] _variables = [];
    private double[] _coefficients = [];
    private double _factor;
    private double _offset;
    private double _intercept;

    /// <summary>Отобранные признаки с их биннингом.</summary>
    public IReadOnlyList<VariableBinning> Variables => _variables;

    /// <summary>Обучает карту на выборке заявок.</summary>
    /// <param name="variableNames">Названия признаков.</param>
    /// <param name="values">Матрица «заявки x признаки».</param>
    /// <param name="defaults">Признак дефолта по каждой заявке.</param>
    /// <param name="options">Настройки отбора и шкалы; при <c>null</c> берутся значения по умолчанию.</param>
    /// <returns>Карта, её качество и список отклонённых признаков.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Данные несогласованы или ни один признак не отобран.</exception>
    public ScorecardResult Fit(
        IReadOnlyList<string> variableNames, Matrix values, IReadOnlyList<bool> defaults,
        ScorecardOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(variableNames);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(defaults);

        options ??= new ScorecardOptions();

        if (values.Height != defaults.Count)
            throw new ArgumentException("Число строк матрицы должно совпадать с числом исходов.", nameof(defaults));

        IReadOnlyList<VariableBinning> all =
            WoeBinning.FitAll(variableNames, values, defaults, options.MaxBins, options.MinBinShare);

        var selected = new List<VariableBinning>();
        var rejected = new List<(string, double, string)>();

        foreach (VariableBinning binning in all)
        {
            if (binning.InformationValue < options.MinInformationValue)
                rejected.Add((binning.Variable, binning.InformationValue, "информационная ценность ниже порога"));
            else if (binning.InformationValue > options.MaxInformationValue)
                rejected.Add((binning.Variable, binning.InformationValue, "подозрение на утечку целевой переменной"));
            else
                selected.Add(binning);
        }

        if (selected.Count == 0)
            throw new ArgumentException(
                "Ни один признак не прошёл отбор по информационной ценности.", nameof(values));

        _variables = [.. selected];

        int n = values.Height;
        int k = _variables.Length;
        var design = new double[n, k + 1];
        var target = new double[n];

        for (int i = 0; i < n; i++)
        {
            design[i, 0] = 1.0;
            for (int j = 0; j < k; j++)
            {
                int column = variableNames.ToList().IndexOf(_variables[j].Variable);
                design[i, j + 1] = _variables[j].Transform(values[i, column]);
            }
            target[i] = defaults[i] ? 1 : 0;
        }

        var model = new LogisticRegression();
        model.Fit(design, target, options.Ridge);

        _intercept = model.Beta[0];
        _coefficients = model.Beta.Skip(1).ToArray();

        _factor = options.PointsToDoubleOdds / Math.Log(2);
        _offset = options.BaseScore - (_factor * Math.Log(options.BaseOdds));

        var probabilities = new Vector(n);
        for (int i = 0; i < n; i++)
        {
            var row = new double[k + 1];
            for (int j = 0; j <= k; j++) row[j] = design[i, j];
            probabilities[i] = model.Predict(row);
        }

        var points = new List<ScorecardPoint>();
        double minScore = 0, maxScore = 0;

        for (int j = 0; j < k; j++)
        {
            double best = double.NegativeInfinity, worst = double.PositiveInfinity;

            foreach (ScoreBin bin in _variables[j].Bins)
            {
                double value = PointsFor(j, bin.Woe, k);
                points.Add(new ScorecardPoint(_variables[j].Variable, bin.Label, bin.Woe, _coefficients[j], value));

                best = Math.Max(best, value);
                worst = Math.Min(worst, value);
            }

            minScore += worst;
            maxScore += best;
        }

        return new ScorecardResult
        {
            Variables = _variables,
            Points = points,
            Rejected = rejected,
            Intercept = _intercept,
            Coefficients = _coefficients,
            BasePoints = _offset,
            PointsToDoubleOdds = options.PointsToDoubleOdds,
            ScoreRange = (minScore, maxScore),
            Quality = ScoreMetrics.Evaluate(probabilities, defaults),
        };
    }

    /// <summary>Балл заявки.</summary>
    /// <param name="applicant">Значения признаков в том же порядке, что при обучении.</param>
    /// <returns>Суммарный балл.</returns>
    /// <exception cref="InvalidOperationException">Карта не обучена.</exception>
    public double Score(IReadOnlyDictionary<string, double> applicant)
    {
        ArgumentNullException.ThrowIfNull(applicant);
        if (_variables.Length == 0) throw new InvalidOperationException("Сначала обучите карту.");

        double score = 0;
        for (int j = 0; j < _variables.Length; j++)
        {
            double value = applicant.TryGetValue(_variables[j].Variable, out double v) ? v : 0;
            score += PointsFor(j, _variables[j].Transform(value), _variables.Length);
        }

        return score;
    }

    /// <summary>Вероятность дефолта по баллу.</summary>
    /// <param name="score">Балл заявки.</param>
    /// <returns>Вероятность дефолта.</returns>
    public double ProbabilityOfDefault(double score)
    {
        double logOdds = (score - _offset) / _factor;
        return 1.0 / (1.0 + Math.Exp(logOdds));
    }

    /// <summary>
    /// Баллы за попадание признака в интервал с заданным весом.
    /// </summary>
    /// <remarks>
    /// Свободный член и базовое смещение шкалы делятся поровну между
    /// признаками, поэтому сумма строк карты сразу даёт итоговый балл
    /// и никакого отдельного слагаемого добавлять не нужно.
    /// </remarks>
    private double PointsFor(int variableIndex, double woe, int variableCount) =>
        (-((_coefficients[variableIndex] * woe) + (_intercept / variableCount)) * _factor)
        + (_offset / variableCount);
}
