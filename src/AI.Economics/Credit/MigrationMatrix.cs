using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Economics.Credit;

/// <summary>Наблюдение перехода заёмщика между рейтингами за период.</summary>
/// <param name="From">Рейтинг на начало периода.</param>
/// <param name="To">Рейтинг на конец периода.</param>
/// <param name="Weight">Вес наблюдения: число заёмщиков или сумма экспозиции.</param>
public sealed record RatingTransition(string From, string To, double Weight = 1.0);

/// <summary>Характеристика одного рейтингового класса в матрице миграции.</summary>
/// <param name="Rating">Рейтинг.</param>
/// <param name="Observations">Суммарный вес наблюдений в классе.</param>
/// <param name="Stability">Вероятность сохранить рейтинг.</param>
/// <param name="UpgradeRate">Вероятность повышения рейтинга.</param>
/// <param name="DowngradeRate">Вероятность понижения без дефолта.</param>
/// <param name="DefaultRate">Вероятность дефолта за период.</param>
public sealed record RatingProfile(
    string Rating, double Observations, double Stability,
    double UpgradeRate, double DowngradeRate, double DefaultRate);

/// <summary>Матрица миграции рейтингов и её характеристики.</summary>
public sealed record MigrationMatrixResult : IInterpretable
{
    /// <summary>Рейтинги в порядке убывания качества; последний считается дефолтным.</summary>
    public IReadOnlyList<string> Ratings { get; init; } = [];

    /// <summary>Матрица вероятностей перехода: строка — рейтинг начала периода.</summary>
    public Matrix Transitions { get; init; } = new(1, 1);

    /// <summary>Матрица весов наблюдений, по которой оценены вероятности.</summary>
    public Matrix Counts { get; init; } = new(1, 1);

    /// <summary>Характеристики рейтинговых классов.</summary>
    public IReadOnlyList<RatingProfile> Profiles { get; init; } = [];

    /// <summary>Индекс дефолтного состояния в списке рейтингов.</summary>
    public int DefaultIndex { get; init; }

    /// <summary>Число периодов наблюдения, по которым построена матрица.</summary>
    public int Periods { get; init; } = 1;

    /// <summary>Средняя устойчивость рейтингов: среднее по диагонали.</summary>
    public double AverageStability =>
        Profiles.Count > 0 ? Profiles.Average(p => p.Stability) : 0;

    /// <summary>Перевес понижений над повышениями: индикатор ухудшения портфеля.</summary>
    public double NetDowngradeDrift =>
        Profiles.Count > 0 ? Profiles.Average(p => p.DowngradeRate + p.DefaultRate - p.UpgradeRate) : 0;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        RatingProfile? riskiest = Profiles
            .Where(p => p.Rating != (Ratings.Count > 0 ? Ratings[DefaultIndex] : null))
            .OrderByDescending(p => p.DefaultRate)
            .FirstOrDefault();

        RatingProfile? leastStable = Profiles
            .Where(p => p.Rating != (Ratings.Count > 0 ? Ratings[DefaultIndex] : null))
            .OrderBy(p => p.Stability)
            .FirstOrDefault();

        bool monotone = IsDefaultRateMonotone();
        double thin = Profiles.Count(p => p.Observations < 30);

        var builder = new InterpretationBuilder("Матрица миграции рейтингов")
            .Summary($"Оценено {Ratings.Count} рейтинговых классов по " +
                     $"{Fmt.Int(Profiles.Sum(p => p.Observations))} наблюдениям за {Periods} " +
                     $"период(ов). Средняя устойчивость рейтинга {Fmt.Pct(AverageStability, 1)}, " +
                     $"перевес понижений над повышениями {Fmt.Pct(NetDowngradeDrift, 2)}.")
            .Metric("Средняя устойчивость", AverageStability, null,
                "вероятность сохранить рейтинг за период",
                AverageStability > 0.8 ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("Дрейф вниз", NetDowngradeDrift, null,
                NetDowngradeDrift > 0 ? "портфель в среднем ухудшается" : "портфель в среднем улучшается",
                NetDowngradeDrift > 0.05 ? MetricQuality.Critical
                    : NetDowngradeDrift > 0 ? MetricQuality.Warning : MetricQuality.Good, 4)
            .Metric("Классов", Ratings.Count, null,
                $"дефолтное состояние — «{(Ratings.Count > 0 ? Ratings[DefaultIndex] : "—")}»",
                MetricQuality.Neutral, 0);

        foreach (RatingProfile profile in Profiles.Where(p => p.Rating != (Ratings.Count > 0 ? Ratings[DefaultIndex] : null)))
        {
            builder.Metric($"PD: {profile.Rating}", profile.DefaultRate, null,
                $"устойчивость {Fmt.Pct(profile.Stability, 1)}, наблюдений {Fmt.Int(profile.Observations)}",
                MetricQuality.Unknown, 4);
        }

        return builder
            .FindingIf(riskiest is not null,
                $"Наибольшая вероятность дефолта за период у класса «{riskiest?.Rating}» — " +
                $"{Fmt.Pct(riskiest?.DefaultRate ?? 0, 2)}.")
            .FindingIf(leastStable is not null,
                $"Наименее устойчив класс «{leastStable?.Rating}»: сохраняет рейтинг только " +
                $"{Fmt.Pct(leastStable?.Stability ?? 0, 1)} заёмщиков.")
            .Finding("Матрица миграции — это не только оценка вероятности дефолта, но и вход " +
                     "для расчёта убытков за весь срок: возведение матрицы в степень даёт " +
                     "кумулятивные вероятности дефолта на любом горизонте.")
            .WarningIf(!monotone,
                "Вероятность дефолта не растёт монотонно с ухудшением рейтинга. Либо шкала " +
                "упорядочена неверно, либо наблюдений в отдельных классах слишком мало.")
            .WarningIf(thin > 0,
                $"В {Fmt.Int(thin)} классах меньше 30 наблюдений. Оценки перехода для них " +
                "неустойчивы и требуют сглаживания либо объединения классов.")
            .Warning("Матрица предполагает марковость: вероятность перехода зависит только " +
                     "от текущего рейтинга. На практике заметны эффекты инерции — недавно " +
                     "понижённые заёмщики понижаются чаще, чем следует из матрицы.")
            .Recommendation("Стройте матрицы отдельно по фазам цикла: усреднение спада и роста " +
                            "даёт матрицу, не описывающую ни одну из фаз.")
            .Recommendation("Проверяйте кумулятивные вероятности дефолта из матрицы против " +
                            "фактических дефолтов винтажей — это лучшая проверка адекватности.")
            .Build();
    }

    /// <summary>Проверяет, растёт ли вероятность дефолта с ухудшением рейтинга.</summary>
    private bool IsDefaultRateMonotone()
    {
        var rates = Profiles
            .Where(p => p.Rating != (Ratings.Count > 0 ? Ratings[DefaultIndex] : null))
            .Select(p => p.DefaultRate)
            .ToList();

        for (int i = 1; i < rates.Count; i++)
            if (rates[i] < rates[i - 1] - 1e-12) return false;

        return true;
    }
}

/// <summary>
/// Матрицы миграции рейтингов: оценка, возведение в степень и кумулятивные
/// вероятности дефолта.
/// </summary>
/// <remarks>
/// <para>
/// Матрица миграции показывает, с какой вероятностью заёмщик рейтинга <c>i</c>
/// окажется в рейтинге <c>j</c> через период. Оценка частотная: доля переходов
/// из класса, нормированная на общее число наблюдений класса.
/// </para>
/// <para>
/// Дефолт трактуется как поглощающее состояние: из него нет выхода. Тогда
/// кумулятивная вероятность дефолта на горизонте <c>T</c> читается прямо из
/// столбца дефолта матрицы, возведённой в степень <c>T</c>. Это стандартный
/// способ получить кривую PD для расчёта убытков за весь срок, когда прямых
/// наблюдений на длинном горизонте недостаточно.
/// </para>
/// <para>
/// Практическое ограничение — предпосылка марковости и однородности во времени.
/// Матрица, усреднённая по фазам цикла, недооценивает дефолты в спад и
/// переоценивает в рост, поэтому регуляторные расчёты обычно требуют
/// отдельных матриц по состояниям экономики.
/// </para>
/// </remarks>
public static class MigrationMatrix
{
    /// <summary>Оценивает матрицу миграции по наблюдённым переходам.</summary>
    /// <param name="ratings">Рейтинги в порядке убывания качества; последний считается дефолтом.</param>
    /// <param name="transitions">Наблюдённые переходы.</param>
    /// <param name="periods">Число периодов, за которые собраны наблюдения.</param>
    /// <returns>Матрица вероятностей и характеристики классов.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Шкала короче двух классов или переходов нет.</exception>
    public static MigrationMatrixResult Estimate(
        IReadOnlyList<string> ratings, IReadOnlyList<RatingTransition> transitions, int periods = 1)
    {
        ArgumentNullException.ThrowIfNull(ratings);
        ArgumentNullException.ThrowIfNull(transitions);

        if (ratings.Count < 2)
            throw new ArgumentException("Нужно как минимум два рейтинговых класса.", nameof(ratings));
        if (transitions.Count == 0)
            throw new ArgumentException("Список переходов пуст.", nameof(transitions));

        int k = ratings.Count;
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < k; i++) index[ratings[i]] = i;

        var counts = new Matrix(k, k);

        foreach (RatingTransition transition in transitions)
        {
            if (!index.TryGetValue(transition.From, out int from) ||
                !index.TryGetValue(transition.To, out int to))
                throw new ArgumentException(
                    $"Переход «{transition.From}» -> «{transition.To}» содержит неизвестный рейтинг.",
                    nameof(transitions));

            counts[from, to] += transition.Weight;
        }

        int defaultIndex = k - 1;
        var probabilities = new Matrix(k, k);
        var profiles = new List<RatingProfile>(k);

        for (int i = 0; i < k; i++)
        {
            double total = 0;
            for (int j = 0; j < k; j++) total += counts[i, j];

            if (i == defaultIndex && total == 0)
            {
                // Дефолт поглощающий: без наблюдений оставляем заёмщика в дефолте.
                probabilities[i, defaultIndex] = 1;
            }
            else if (total == 0)
            {
                probabilities[i, i] = 1;
            }
            else
            {
                for (int j = 0; j < k; j++) probabilities[i, j] = counts[i, j] / total;
            }

            double upgrade = 0, downgrade = 0;
            for (int j = 0; j < k; j++)
            {
                if (j == i || j == defaultIndex) continue;
                if (j < i) upgrade += probabilities[i, j];
                else downgrade += probabilities[i, j];
            }

            profiles.Add(new RatingProfile(
                ratings[i], total, probabilities[i, i], upgrade, downgrade,
                i == defaultIndex ? 0 : probabilities[i, defaultIndex]));
        }

        return new MigrationMatrixResult
        {
            Ratings = ratings,
            Transitions = probabilities,
            Counts = counts,
            Profiles = profiles,
            DefaultIndex = defaultIndex,
            Periods = Math.Max(1, periods),
        };
    }

    /// <summary>Возводит матрицу миграции в степень: переход за несколько периодов.</summary>
    /// <param name="result">Оценённая матрица.</param>
    /// <param name="periods">Число периодов.</param>
    /// <returns>Матрица перехода за указанное число периодов.</returns>
    /// <exception cref="ArgumentNullException">Матрица не задана.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Число периодов меньше единицы.</exception>
    public static Matrix MultiPeriod(MigrationMatrixResult result, int periods)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfLessThan(periods, 1);

        int k = result.Ratings.Count;
        var power = new Matrix(k, k);
        for (int i = 0; i < k; i++) power[i, i] = 1;

        for (int step = 0; step < periods; step++)
            power = Multiply(power, result.Transitions);

        return power;
    }

    /// <summary>Кумулятивные вероятности дефолта по горизонтам.</summary>
    /// <param name="result">Оценённая матрица.</param>
    /// <param name="horizon">Максимальный горизонт в периодах.</param>
    /// <returns>Для каждого рейтинга — вероятности дефолта на горизонтах от одного периода до заданного.</returns>
    /// <exception cref="ArgumentNullException">Матрица не задана.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Горизонт меньше единицы.</exception>
    public static IReadOnlyList<Vector> CumulativeDefault(MigrationMatrixResult result, int horizon)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfLessThan(horizon, 1);

        int k = result.Ratings.Count;
        Matrix absorbing = Absorbing(result);
        var power = new Matrix(k, k);
        for (int i = 0; i < k; i++) power[i, i] = 1;

        var curves = new List<Vector>(k);
        for (int i = 0; i < k; i++) curves.Add(new Vector(horizon));

        for (int t = 0; t < horizon; t++)
        {
            power = Multiply(power, absorbing);
            for (int i = 0; i < k; i++) curves[i][t] = power[i, result.DefaultIndex];
        }

        return curves;
    }

    /// <summary>Стационарное распределение цепи без учёта поглощения в дефолте.</summary>
    /// <param name="result">Оценённая матрица.</param>
    /// <param name="iterations">Число итераций степенного метода.</param>
    /// <returns>Доли рейтингов в равновесии.</returns>
    /// <exception cref="ArgumentNullException">Матрица не задана.</exception>
    public static Vector StationaryDistribution(MigrationMatrixResult result, int iterations = 500)
    {
        ArgumentNullException.ThrowIfNull(result);

        int k = result.Ratings.Count;
        var state = new Vector(k);
        for (int i = 0; i < k; i++) state[i] = 1.0 / k;

        for (int step = 0; step < iterations; step++)
        {
            var next = new Vector(k);
            for (int j = 0; j < k; j++)
                for (int i = 0; i < k; i++)
                    next[j] += state[i] * result.Transitions[i, j];

            double sum = next.Sum();
            if (sum <= 0) break;

            double shift = 0;
            for (int i = 0; i < k; i++)
            {
                next[i] /= sum;
                shift += Math.Abs(next[i] - state[i]);
            }

            state = next;
            if (shift < 1e-12) break;
        }

        return state;
    }

    /// <summary>Копия матрицы с поглощающим дефолтом.</summary>
    private static Matrix Absorbing(MigrationMatrixResult result)
    {
        int k = result.Ratings.Count;
        var matrix = new Matrix(k, k);

        for (int i = 0; i < k; i++)
            for (int j = 0; j < k; j++)
                matrix[i, j] = result.Transitions[i, j];

        for (int j = 0; j < k; j++) matrix[result.DefaultIndex, j] = 0;
        matrix[result.DefaultIndex, result.DefaultIndex] = 1;

        return matrix;
    }

    /// <summary>Произведение двух квадратных матриц.</summary>
    private static Matrix Multiply(Matrix left, Matrix right)
    {
        int k = left.Height;
        var product = new Matrix(k, k);

        for (int i = 0; i < k; i++)
            for (int j = 0; j < k; j++)
            {
                double sum = 0;
                for (int m = 0; m < k; m++) sum += left[i, m] * right[m, j];
                product[i, j] = sum;
            }

        return product;
    }
}
