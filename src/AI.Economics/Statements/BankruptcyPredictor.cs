using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Credit;
using AI.Insights;
using AI.Economics.Numerics;
using AI.ML.Classification;
using AI.Statistics;

namespace AI.Economics.Statements;

/// <summary>Модель, на которой строится предсказание банкротства.</summary>
public enum BankruptcyModelKind
{
    /// <summary>Логистическая регрессия: интерпретируемая базовая линия.</summary>
    Logistic,

    /// <summary>Байесовский классификатор фреймворка.</summary>
    Bayesian,

    /// <summary>Машина опорных векторов фреймворка.</summary>
    SupportVector,
}

/// <summary>Наблюдение обучающей выборки: отчётность и исход.</summary>
/// <param name="Statement">Отчётность компании.</param>
/// <param name="WentBankrupt">Наступило ли банкротство в горизонте наблюдения.</param>
public sealed record BankruptcyObservation(FinancialStatement Statement, bool WentBankrupt);

/// <summary>Важность признака, оценённая перестановочным методом.</summary>
/// <param name="Feature">Название признака.</param>
/// <param name="Importance">Падение площади под кривой при перемешивании признака.</param>
/// <param name="MeanHealthy">Среднее значение у выживших компаний.</param>
/// <param name="MeanBankrupt">Среднее значение у обанкротившихся.</param>
public sealed record FeatureImportance(
    string Feature, double Importance, double MeanHealthy, double MeanBankrupt);

/// <summary>Результат обучения модели предсказания банкротства.</summary>
public sealed record BankruptcyModelResult : IInterpretable
{
    /// <summary>Использованная модель.</summary>
    public BankruptcyModelKind Model { get; init; }

    /// <summary>Число наблюдений в обучающей выборке.</summary>
    public int Observations { get; init; }

    /// <summary>Число банкротств в выборке.</summary>
    public int Bankruptcies { get; init; }

    /// <summary>Качество на обучающей выборке.</summary>
    public ScoreQuality InSample { get; init; } = new();

    /// <summary>Качество на скользящем контроле.</summary>
    public ScoreQuality CrossValidated { get; init; } = new();

    /// <summary>Число блоков скользящего контроля.</summary>
    public int Folds { get; init; }

    /// <summary>Важности признаков по убыванию.</summary>
    public IReadOnlyList<FeatureImportance> Importances { get; init; } = [];

    /// <summary>Разрыв между обучающей выборкой и контролем: мера переобучения.</summary>
    public double OverfitGap => InSample.Gini - CrossValidated.Gini;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        FeatureImportance? top = Importances.FirstOrDefault();
        double bankruptcyRate = Observations > 0 ? (double)Bankruptcies / Observations : 0;
        var useless = Importances.Where(i => i.Importance <= 0.001).ToList();

        var builder = new InterpretationBuilder($"Предсказание банкротства: модель {ModelName()}")
            .Summary($"Обучено на {Observations} наблюдениях, из них {Bankruptcies} банкротств " +
                     $"({Fmt.Pct(bankruptcyRate, 1)}). Коэффициент Джини на скользящем контроле " +
                     $"{Fmt.Num(CrossValidated.Gini, 3)} против {Fmt.Num(InSample.Gini, 3)} " +
                     $"на обучающей выборке; разрыв {Fmt.Num(OverfitGap, 3)}.")
            .Metric("Джини на контроле", CrossValidated.Gini, null,
                $"по {Folds} блокам скользящего контроля",
                CrossValidated.Gini > 0.5 ? MetricQuality.Good
                    : CrossValidated.Gini > 0.3 ? MetricQuality.Warning : MetricQuality.Critical, 3)
            .Metric("Джини на обучении", InSample.Gini, null,
                "оптимистичная оценка", MetricQuality.Neutral, 3)
            .Metric("Разрыв обучение-контроль", OverfitGap, null,
                OverfitGap > 0.15 ? "модель переобучена" : "переобучение умеренное",
                OverfitGap > 0.15 ? MetricQuality.Critical
                    : OverfitGap > 0.07 ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Статистика Колмогорова-Смирнова", CrossValidated.Ks, null,
                "максимальный разрыв между распределениями исходов",
                CrossValidated.Ks > 0.3 ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("Брайер", CrossValidated.Brier, null,
                "средняя квадратичная ошибка вероятности", MetricQuality.Neutral, 4)
            .Metric("Наклон калибровки", CrossValidated.CalibrationSlope, null,
                Math.Abs(CrossValidated.CalibrationSlope - 1) < 0.25
                    ? "вероятности откалиброваны"
                    : "вероятности смещены и требуют перекалибровки",
                Math.Abs(CrossValidated.CalibrationSlope - 1) < 0.25
                    ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("Доля банкротств", bankruptcyRate, null,
                $"{Bankruptcies} из {Observations}",
                bankruptcyRate is > 0.02 and < 0.5 ? MetricQuality.Good : MetricQuality.Warning, 3);

        foreach (FeatureImportance importance in Importances)
        {
            builder.Metric($"Важность: {importance.Feature}", importance.Importance, null,
                $"среднее у выживших {Fmt.Num(importance.MeanHealthy, 3)}, " +
                $"у банкротов {Fmt.Num(importance.MeanBankrupt, 3)}",
                MetricQuality.Unknown, 4);
        }

        return builder
            .FindingIf(top is not null,
                $"Сильнее всего на прогноз влияет признак «{top?.Feature}»: перемешивание " +
                $"его значений снижает площадь под кривой на {Fmt.Num(top?.Importance ?? 0, 4)}. " +
                $"У выживших компаний он в среднем {Fmt.Num(top?.MeanHealthy ?? 0, 3)}, " +
                $"у обанкротившихся {Fmt.Num(top?.MeanBankrupt ?? 0, 3)}.")
            .Finding("Оценивать модель нужно по скользящему контролю: на обучающей выборке " +
                     "любая модель с достаточной гибкостью покажет высокое качество, " +
                     "и разрыв между двумя оценками — это и есть мера переобучения.")
            .FindingIf(useless.Count > 0,
                $"Признаков без вклада в прогноз: {useless.Count}. Их удаление упростит " +
                "модель, не ухудшив качество.")
            .WarningIf(Bankruptcies < 30,
                $"Банкротств в выборке всего {Bankruptcies}. При таком числе редких событий " +
                "оценки качества имеют широкий доверительный интервал, и разница между " +
                "моделями чаще всего статистически незначима.")
            .WarningIf(OverfitGap > 0.15,
                $"Разрыв между обучением и контролем {Fmt.Num(OverfitGap, 3)}: модель " +
                "запоминает выборку. Уменьшите число признаков или усильте регуляризацию.")
            .Warning("Банкротство — редкое событие, и обучающая выборка почти всегда смещена: " +
                     "в неё попадают компании, дожившие до сдачи отчётности. Абсолютные " +
                     "вероятности из такой модели требуют перекалибровки на реальную " +
                     "частоту банкротств в популяции.")
            .Recommendation("Сравнивайте модель не с нулём, а с баллом Альтмана на тех же " +
                            "данных: прирост качества относительно классической формулы — " +
                            "единственное честное обоснование сложной модели.")
            .Recommendation("Проверяйте модель на более позднем периоде, а не только на " +
                            "случайной подвыборке: отчётность и структура экономики меняются, " +
                            "и случайный контроль это не улавливает.")
            .Build();
    }

    /// <summary>Читаемое имя модели.</summary>
    private string ModelName() => Model switch
    {
        BankruptcyModelKind.Logistic => "логистическая регрессия",
        BankruptcyModelKind.Bayesian => "байесовский классификатор",
        _ => "машина опорных векторов",
    };
}

/// <summary>Прогноз банкротства для отдельной компании.</summary>
public sealed record BankruptcyPrediction : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Отчётный период.</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Вероятность банкротства по модели.</summary>
    public double Probability { get; init; }

    /// <summary>Порог отнесения к группе риска.</summary>
    public double Threshold { get; init; } = 0.5;

    /// <summary>Относит ли модель компанию к группе риска.</summary>
    public bool IsHighRisk => Probability >= Threshold;

    /// <summary>Значения признаков компании.</summary>
    public IReadOnlyList<(string Feature, double Value, double MeanHealthy, double MeanBankrupt)> Features { get; init; } = [];

    /// <summary>Балл Альтмана на тех же данных для сравнения.</summary>
    public double AltmanZ { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var alarming = Features
            .Where(f => Math.Abs(f.MeanBankrupt - f.MeanHealthy) > 1e-9)
            .Where(f => Math.Abs(f.Value - f.MeanBankrupt) < Math.Abs(f.Value - f.MeanHealthy))
            .ToList();

        var builder = new InterpretationBuilder($"Прогноз банкротства: {Company}, {Period}")
            .Summary($"Вероятность банкротства {Fmt.Pct(Probability, 2)} при пороге " +
                     $"{Fmt.Pct(Threshold, 0)} — компания " +
                     (IsHighRisk ? "отнесена к группе риска" : "вне группы риска") + ". " +
                     $"Балл Альтмана на тех же данных {Fmt.Num(AltmanZ, 2)}. " +
                     $"К профилю банкротов ближе {alarming.Count} признаков из {Features.Count}.")
            .Metric("Вероятность банкротства", Probability, null,
                IsHighRisk ? "выше порога отнесения к риску" : "ниже порога",
                Probability > 0.5 ? MetricQuality.Critical
                    : Probability > 0.2 ? MetricQuality.Warning : MetricQuality.Good, 4)
            .Metric("Балл Альтмана", AltmanZ, null,
                "классическая модель на тех же данных для сравнения",
                AltmanZ > 2.99 ? MetricQuality.Good
                    : AltmanZ > 1.81 ? MetricQuality.Warning : MetricQuality.Critical, 2)
            .Metric("Признаков в зоне риска", alarming.Count, null,
                $"из {Features.Count} рассчитанных",
                alarming.Count <= 2 ? MetricQuality.Good
                    : alarming.Count <= 5 ? MetricQuality.Warning : MetricQuality.Critical, 0);

        foreach ((string feature, double value, double healthy, double bankrupt) in Features)
        {
            builder.Metric(feature, value, null,
                $"среднее у выживших {Fmt.Num(healthy, 3)}, у банкротов {Fmt.Num(bankrupt, 3)}",
                Math.Abs(value - bankrupt) < Math.Abs(value - healthy)
                    ? MetricQuality.Warning : MetricQuality.Good, 3);
        }

        return builder
            .FindingIf(alarming.Count > 0,
                $"К профилю обанкротившихся компаний ближе всего признаки: " +
                $"{string.Join(", ", alarming.Take(3).Select(f => f.Feature))}.")
            .FindingIf(IsHighRisk != (AltmanZ < 1.81),
                "Модель и балл Альтмана дают разные ответы. Расхождение само по себе " +
                "информативно: оно означает, что решение опирается на признаки, " +
                "которых нет в классической формуле.")
            .Finding("Вероятность из модели — это ранжирующая оценка. Для решения важен " +
                     "не сам её уровень, а место компании в распределении: порог назначается " +
                     "исходя из цены пропуска банкротства и цены ложной тревоги.")
            .WarningIf(Probability > 0.2 && Probability < 0.5,
                "Компания в промежуточной зоне: модель не даёт уверенного ответа. " +
                "Такие случаи имеет смысл разбирать вручную, а не по автоматическому правилу.")
            .Warning("Прогноз построен на одной отчётной дате. Для редкого события " +
                     "надёжнее опираться на динамику вероятности за несколько периодов: " +
                     "устойчивый рост оценки информативнее её уровня.")
            .Recommendation("Сопоставьте прогноз с моделями банкротства и качеством прибыли: " +
                            "согласие независимых подходов повышает обоснованность решения.")
            .Recommendation("Порог отсечения подбирайте по цене ошибок, а не по стандартным " +
                            "0,5: для кредитора цена пропущенного банкротства обычно " +
                            "на порядок выше цены отказа хорошему заёмщику.")
            .Build();
    }
}

/// <summary>
/// Предсказание банкротства на классификаторах фреймворка.
/// </summary>
/// <remarks>
/// <para>
/// Из отчётности извлекается набор признаков, повторяющий логику классических
/// моделей банкротства, но дополненный качеством прибыли и покрытием долга
/// денежным потоком. Признаки стандартизируются по обучающей выборке, после
/// чего обучается один из классификаторов: логистическая регрессия, байесовский
/// классификатор или машина опорных векторов из <c>AI.ML</c>.
/// </para>
/// <para>
/// Качество оценивается на стратифицированном скользящем контроле: доля
/// банкротств в каждом блоке сохраняется, а вероятности собираются вне
/// обучения. Разрыв между качеством на обучающей выборке и на контроле —
/// прямая мера переобучения.
/// </para>
/// <para>
/// Важность признаков считается перестановочным методом: значения одного
/// признака перемешиваются, и измеряется падение площади под кривой. Метод
/// не зависит от устройства модели, поэтому одинаково применим ко всем трём
/// классификаторам.
/// </para>
/// </remarks>
public sealed class BankruptcyPredictor
{
    private readonly List<string> _features =
    [
        "Рабочий капитал к активам",
        "Нераспределённая прибыль к активам",
        "Операционная прибыль к активам",
        "Капитал к обязательствам",
        "Оборачиваемость активов",
        "Текущая ликвидность",
        "Чистая прибыль к активам",
        "Денежный поток к обязательствам",
        "Долг к прибыли до амортизации",
        "Начисления к активам",
        "Покрытие процентов",
    ];

    private IClassifier? _classifier;
    private LogisticRegression? _logistic;
    private BankruptcyModelKind _kind;
    private double[] _means = [];
    private double[] _deviations = [];
    private double[] _meansHealthy = [];
    private double[] _meansBankrupt = [];

    /// <summary>Названия признаков в порядке их следования в векторе.</summary>
    public IReadOnlyList<string> FeatureNames => _features;

    /// <summary>Обучена ли модель.</summary>
    public bool IsTrained => _classifier is not null || _logistic is not null;

    /// <summary>Обучает модель на исторических наблюдениях.</summary>
    /// <param name="observations">Отчётность компаний с известным исходом.</param>
    /// <param name="kind">Тип модели.</param>
    /// <param name="folds">Число блоков скользящего контроля.</param>
    /// <param name="seed">Зерно генератора для перемешивания и перестановочной важности.</param>
    /// <returns>Качество модели и важности признаков.</returns>
    /// <exception cref="ArgumentNullException">Наблюдения не заданы.</exception>
    /// <exception cref="ArgumentException">В выборке нет обоих исходов или слишком мало наблюдений.</exception>
    public BankruptcyModelResult Train(
        IReadOnlyList<BankruptcyObservation> observations,
        BankruptcyModelKind kind = BankruptcyModelKind.Logistic,
        int folds = 5,
        int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(observations);

        if (observations.Count < 20)
            throw new ArgumentException("Нужно как минимум двадцать наблюдений.", nameof(observations));

        var labels = observations.Select(o => o.WentBankrupt).ToList();
        if (labels.All(l => l) || labels.All(l => !l))
            throw new ArgumentException(
                "В выборке должны присутствовать оба исхода.", nameof(observations));

        double[][] raw = [.. observations.Select(o => RawFeatures(o.Statement))];

        _kind = kind;
        Standardize(raw, labels);

        Vector[] design = [.. raw.Select(Scale)];
        int[] targets = [.. labels.Select(l => l ? 1 : 0)];

        FitModel(design, targets, kind);

        var inSample = new Vector(design.Length);
        for (int i = 0; i < design.Length; i++) inSample[i] = Probability(design[i]);

        Vector crossValidated = CrossValidate(design, targets, Math.Max(2, folds), seed);

        return new BankruptcyModelResult
        {
            Model = kind,
            Observations = observations.Count,
            Bankruptcies = labels.Count(l => l),
            InSample = ScoreMetrics.Evaluate(inSample, labels),
            CrossValidated = ScoreMetrics.Evaluate(crossValidated, labels),
            Folds = Math.Max(2, folds),
            Importances = PermutationImportance(design, labels, seed),
        };
    }

    /// <summary>Строит прогноз для компании.</summary>
    /// <param name="statement">Отчётность компании.</param>
    /// <param name="threshold">Порог отнесения к группе риска.</param>
    /// <returns>Вероятность банкротства и профиль признаков.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    /// <exception cref="InvalidOperationException">Модель не обучена.</exception>
    public BankruptcyPrediction Predict(FinancialStatement statement, double threshold = 0.5)
    {
        ArgumentNullException.ThrowIfNull(statement);
        if (!IsTrained) throw new InvalidOperationException("Сначала обучите модель.");

        double[] raw = RawFeatures(statement);
        double probability = Probability(Scale(raw));

        var profile = new List<(string, double, double, double)>(_features.Count);
        for (int j = 0; j < _features.Count; j++)
            profile.Add((_features[j], raw[j], _meansHealthy[j], _meansBankrupt[j]));

        return new BankruptcyPrediction
        {
            Company = statement.Company,
            Period = statement.Period,
            Probability = probability,
            Threshold = threshold,
            Features = profile,
            AltmanZ = DistressScores.Altman(statement).Value,
        };
    }

    /// <summary>Обучает все доступные модели и сравнивает их по скользящему контролю.</summary>
    /// <param name="observations">Обучающая выборка.</param>
    /// <param name="folds">Число блоков скользящего контроля.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Результаты по каждой модели в порядке убывания качества на контроле.</returns>
    /// <exception cref="ArgumentNullException">Наблюдения не заданы.</exception>
    public static IReadOnlyList<BankruptcyModelResult> CompareAll(
        IReadOnlyList<BankruptcyObservation> observations, int folds = 5, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var results = new List<BankruptcyModelResult>();

        foreach (BankruptcyModelKind kind in Enum.GetValues<BankruptcyModelKind>())
        {
            var predictor = new BankruptcyPredictor();
            results.Add(predictor.Train(observations, kind, folds, seed));
        }

        return [.. results.OrderByDescending(r => r.CrossValidated.Gini)];
    }

    /// <summary>Извлекает признаки из отчётности до стандартизации.</summary>
    /// <param name="s">Отчётность.</param>
    /// <returns>Вектор признаков в порядке <see cref="FeatureNames"/>.</returns>
    /// <exception cref="ArgumentNullException">Отчётность не задана.</exception>
    /// <exception cref="ArgumentException">Активы неположительны.</exception>
    public static Vector ExtractFeatures(FinancialStatement s) => new(RawFeatures(s));

    /// <summary>Признаки компании с ограничением выбросов.</summary>
    private static double[] RawFeatures(FinancialStatement s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.TotalAssets <= 0)
            throw new ArgumentException("Активы должны быть положительными.", nameof(s));

        double assets = s.TotalAssets;
        double liabilities = Math.Max(s.TotalLiabilities, 1e-9);

        return
        [
            EconMath.Clamp(s.WorkingCapital / assets, -2, 2),
            EconMath.Clamp(s.RetainedEarnings / assets, -3, 2),
            EconMath.Clamp(s.OperatingIncome / assets, -2, 2),
            EconMath.Clamp(s.Equity / liabilities, -5, 20),
            EconMath.Clamp(s.Revenue / assets, 0, 10),
            EconMath.Clamp(s.CurrentLiabilities > 0 ? s.CurrentAssets / s.CurrentLiabilities : 5, 0, 10),
            EconMath.Clamp(s.NetIncome / assets, -2, 2),
            EconMath.Clamp(s.OperatingCashFlow / liabilities, -5, 5),
            EconMath.Clamp(s.Ebitda > 0 ? s.TotalDebt / s.Ebitda : 20, -20, 20),
            EconMath.Clamp((s.NetIncome - s.OperatingCashFlow) / assets, -2, 2),
            EconMath.Clamp(s.InterestExpense > 0 ? s.OperatingIncome / s.InterestExpense : 20, -20, 20),
        ];
    }

    /// <summary>Считает средние и стандартные отклонения обучающей выборки.</summary>
    private void Standardize(double[][] raw, IReadOnlyList<bool> labels)
    {
        int k = _features.Count;
        _means = new double[k];
        _deviations = new double[k];
        _meansHealthy = new double[k];
        _meansBankrupt = new double[k];

        for (int j = 0; j < k; j++)
        {
            double mean = raw.Average(r => r[j]);
            double variance = raw.Length > 1
                ? raw.Sum(r => (r[j] - mean) * (r[j] - mean)) / (raw.Length - 1)
                : 1;

            _means[j] = mean;
            _deviations[j] = Math.Max(Math.Sqrt(variance), 1e-9);

            var healthy = raw.Where((_, i) => !labels[i]).Select(r => r[j]).ToList();
            var bankrupt = raw.Where((_, i) => labels[i]).Select(r => r[j]).ToList();

            _meansHealthy[j] = healthy.Count > 0 ? healthy.Average() : mean;
            _meansBankrupt[j] = bankrupt.Count > 0 ? bankrupt.Average() : mean;
        }
    }

    /// <summary>Приводит признаки к стандартизованной шкале обучающей выборки.</summary>
    private Vector Scale(double[] raw)
    {
        var scaled = new Vector(raw.Length);
        for (int j = 0; j < raw.Length; j++) scaled[j] = (raw[j] - _means[j]) / _deviations[j];
        return scaled;
    }

    /// <summary>Обучает выбранную модель.</summary>
    private void FitModel(Vector[] design, int[] targets, BankruptcyModelKind kind)
    {
        _classifier = null;
        _logistic = null;

        if (kind == BankruptcyModelKind.Logistic)
        {
            var matrix = new double[design.Length, design[0].Count + 1];
            var response = new double[design.Length];

            for (int i = 0; i < design.Length; i++)
            {
                matrix[i, 0] = 1;
                for (int j = 0; j < design[i].Count; j++) matrix[i, j + 1] = design[i][j];
                response[i] = targets[i];
            }

            _logistic = new LogisticRegression();
            _logistic.Fit(matrix, response, 1e-2);
            return;
        }

        IClassifier classifier = kind == BankruptcyModelKind.Bayesian
            ? new BayesianClassifier()
            : new SVMBinary(design[0].Count) { EpochesToPass = 200, LearningRate = 0.05 };

        classifier.Train(design, targets);
        _classifier = classifier;
    }

    /// <summary>Вероятность банкротства для стандартизованного вектора.</summary>
    private double Probability(Vector scaled)
    {
        if (_logistic is not null)
        {
            var row = new double[scaled.Count + 1];
            row[0] = 1;
            for (int j = 0; j < scaled.Count; j++) row[j + 1] = scaled[j];
            return _logistic.Predict(row);
        }

        if (_classifier is null) throw new InvalidOperationException("Сначала обучите модель.");

        Vector probabilities = _classifier.ClassifyProbVector(scaled);
        double probability = probabilities.Count > 1 ? probabilities[1] : probabilities[0];

        return EconMath.Clamp(probability, 1e-9, 1 - 1e-9);
    }

    /// <summary>Собирает вероятности вне обучения по стратифицированным блокам.</summary>
    private Vector CrossValidate(Vector[] design, int[] targets, int folds, int seed)
    {
        Random rng = RandomEngine.Create(seed);
        var assignment = new int[design.Length];

        // Стратификация: положительные и отрицательные раскладываются по блокам отдельно,
        // иначе в блоке может не оказаться ни одного банкротства.
        foreach (int label in new[] { 0, 1 })
        {
            var indices = Enumerable.Range(0, design.Length).Where(i => targets[i] == label).ToList();

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            for (int position = 0; position < indices.Count; position++)
                assignment[indices[position]] = position % folds;
        }

        var outOfFold = new Vector(design.Length);
        var snapshot = (_classifier, _logistic);

        for (int fold = 0; fold < folds; fold++)
        {
            var trainIndices = Enumerable.Range(0, design.Length).Where(i => assignment[i] != fold).ToList();
            var testIndices = Enumerable.Range(0, design.Length).Where(i => assignment[i] == fold).ToList();

            if (testIndices.Count == 0) continue;

            int[] foldTargets = [.. trainIndices.Select(i => targets[i])];
            if (foldTargets.Distinct().Count() < 2)
            {
                foreach (int i in testIndices) outOfFold[i] = foldTargets.Length > 0 ? foldTargets[0] : 0.5;
                continue;
            }

            FitModel([.. trainIndices.Select(i => design[i])], foldTargets, _kind);

            foreach (int i in testIndices) outOfFold[i] = Probability(design[i]);
        }

        // Возвращаем модель, обученную на всей выборке.
        (_classifier, _logistic) = snapshot;

        return outOfFold;
    }

    /// <summary>Перестановочная важность признаков по падению площади под кривой.</summary>
    private IReadOnlyList<FeatureImportance> PermutationImportance(
        Vector[] design, IReadOnlyList<bool> labels, int seed)
    {
        var baseline = new Vector(design.Length);
        for (int i = 0; i < design.Length; i++) baseline[i] = Probability(design[i]);

        double baselineAuc = ScoreMetrics.Evaluate(baseline, labels).Auc;
        Random rng = RandomEngine.Create(seed + 1);
        var importances = new List<FeatureImportance>(_features.Count);

        for (int j = 0; j < _features.Count; j++)
        {
            var order = Enumerable.Range(0, design.Length).ToList();
            for (int i = order.Count - 1; i > 0; i--)
            {
                int k = rng.Next(i + 1);
                (order[i], order[k]) = (order[k], order[i]);
            }

            var permuted = new Vector(design.Length);
            for (int i = 0; i < design.Length; i++)
            {
                var copy = new Vector(design[i].Count);
                for (int f = 0; f < design[i].Count; f++) copy[f] = design[i][f];
                copy[j] = design[order[i]][j];

                permuted[i] = Probability(copy);
            }

            double auc = ScoreMetrics.Evaluate(permuted, labels).Auc;

            importances.Add(new FeatureImportance(
                _features[j], Math.Max(0, baselineAuc - auc), _meansHealthy[j], _meansBankrupt[j]));
        }

        return [.. importances.OrderByDescending(i => i.Importance)];
    }
}
