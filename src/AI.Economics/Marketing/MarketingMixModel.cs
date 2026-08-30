using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Marketing;

/// <summary>Канал продвижения с историей затрат по периодам.</summary>
public sealed record MediaChannel
{
    /// <summary>Название канала.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Затраты по периодам в той же сетке, что и продажи.</summary>
    public Vector Spend { get; init; } = new Vector(0);
}

/// <summary>Вход маркетинг-микс модели.</summary>
public sealed record MmmInput
{
    /// <summary>Продажи по периодам — то, что объясняем.</summary>
    public Vector Sales { get; init; } = new Vector(0);

    /// <summary>Каналы продвижения.</summary>
    public IReadOnlyList<MediaChannel> Channels { get; init; } = [];

    /// <summary>Контрольные переменные: цена, дистрибуция, активность конкурентов.</summary>
    public IReadOnlyList<MediaChannel> Controls { get; init; } = [];

    /// <summary>Длина сезонного цикла в периодах; 0 отключает сезонность.</summary>
    public int SeasonalPeriod { get; init; } = 52;

    /// <summary>Число гармоник Фурье для описания сезонности.</summary>
    public int FourierTerms { get; init; } = 2;

    /// <summary>Включать ли линейный тренд.</summary>
    public bool IncludeTrend { get; init; } = true;

    /// <summary>Коэффициент гребневой регуляризации.</summary>
    public double Ridge { get; init; } = 1.0;

    /// <summary>Доля маржи в продажах — для пересчёта вклада в прибыль.</summary>
    public double MarginRate { get; init; } = 1.0;

    /// <summary>Число итераций подбора гиперпараметров преобразований.</summary>
    public int TuningIterations { get; init; } = 3000;
}

/// <summary>Оценённый эффект одного канала.</summary>
public sealed record ChannelEffect
{
    /// <summary>Название канала.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Коэффициент затухания adstock.</summary>
    public double Decay { get; init; }

    /// <summary>Период полураспада эффекта, в периодах.</summary>
    public double HalfLife => Decay > 0 && Decay < 1 ? Math.Log(0.5) / Math.Log(Decay) : 0;

    /// <summary>Точка половинного насыщения кривой Хилла в единицах накопленных затрат.</summary>
    public double SaturationPoint { get; init; }

    /// <summary>Показатель крутизны кривой насыщения.</summary>
    public double SaturationShape { get; init; }

    /// <summary>Коэффициент регрессии при преобразованном канале.</summary>
    public double Coefficient { get; init; }

    /// <summary>Суммарный вклад канала в продажи за весь период.</summary>
    public double TotalContribution { get; init; }

    /// <summary>Доля канала в общих продажах.</summary>
    public double ContributionShare { get; init; }

    /// <summary>Суммарные затраты на канал.</summary>
    public double TotalSpend { get; init; }

    /// <summary>Возврат на вложенное по марже: вклад в прибыль делить на затраты.</summary>
    public double Roi { get; init; }

    /// <summary>
    /// Предельный возврат: прибыль от следующего рубля в канал. Именно он,
    /// а не средний ROI, отвечает на вопрос о перераспределении бюджета.
    /// </summary>
    public double MarginalRoi { get; init; }

    /// <summary>
    /// Насколько канал близок к насыщению: отношение накопленных затрат
    /// к точке половинного насыщения.
    /// </summary>
    public double SaturationLevel { get; init; }

    /// <summary>Вклад канала по периодам.</summary>
    public Vector Contribution { get; init; } = new Vector(0);
}

/// <summary>Результат маркетинг-микс модели.</summary>
public sealed record MmmResult : IInterpretable
{
    /// <summary>Эффекты каналов по убыванию вклада.</summary>
    public IReadOnlyList<ChannelEffect> Channels { get; init; } = [];

    /// <summary>Базовые продажи: всё, что не объясняется рекламой.</summary>
    public double BaselineContribution { get; init; }

    /// <summary>Суммарные фактические продажи.</summary>
    public double TotalSales { get; init; }

    /// <summary>Доля продаж, объяснённая рекламой.</summary>
    public double MediaShare { get; init; }

    /// <summary>Коэффициент детерминации.</summary>
    public double RSquared { get; init; }

    /// <summary>Средняя абсолютная процентная ошибка посадки.</summary>
    public double Mape { get; init; }

    /// <summary>Модельные продажи по периодам.</summary>
    public Vector Fitted { get; init; } = new Vector(0);

    /// <summary>Базовая линия по периодам.</summary>
    public Vector Baseline { get; init; } = new Vector(0);

    /// <summary>Остатки модели.</summary>
    public Vector Residuals { get; init; } = new Vector(0);

    /// <summary>Доля маржи, использованная при расчёте ROI.</summary>
    public double MarginRate { get; init; } = 1.0;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        ChannelEffect? best = Channels.OrderByDescending(c => c.MarginalRoi).FirstOrDefault();
        ChannelEffect? worst = Channels.OrderBy(c => c.MarginalRoi).FirstOrDefault();
        var saturated = Channels.Where(c => c.SaturationLevel > 2).ToList();
        var unprofitable = Channels.Where(c => c.MarginalRoi < 1).ToList();
        int negative = Channels.Count(c => c.Coefficient < 0);

        var builder = new InterpretationBuilder("Маркетинг-микс: декомпозиция продаж")
            .Summary($"Реклама объясняет {Fmt.Pct(MediaShare)} продаж, остальное — базовая линия " +
                     $"(бренд, дистрибуция, сезон). Лучший канал по предельной отдаче — " +
                     $"«{best?.Name}» ({Fmt.Num(best?.MarginalRoi ?? 0)} рубля прибыли на рубль), " +
                     $"худший — «{worst?.Name}» ({Fmt.Num(worst?.MarginalRoi ?? 0)}).")
            .Metric("R2", RSquared, null, "доля объяснённой дисперсии продаж",
                RSquared > 0.8 ? MetricQuality.Good : RSquared > 0.6 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("MAPE", Fmt.Pct(Mape), null, "средняя ошибка посадки",
                Mape < 0.1 ? MetricQuality.Good : Mape < 0.2 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Вклад рекламы", Fmt.Pct(MediaShare), null, "остальное — базовая линия",
                MediaShare is > 0.05 and < 0.6 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Базовые продажи", Fmt.Money(BaselineContribution), null,
                "продажи без рекламной поддержки");

        foreach (ChannelEffect channel in Channels)
        {
            builder.Metric($"ROI: {channel.Name}", channel.Roi, null,
                $"предельный {Fmt.Num(channel.MarginalRoi)}, полураспад " +
                $"{Fmt.Num(channel.HalfLife, 1)} периодов",
                channel.MarginalRoi >= 1 ? MetricQuality.Good : MetricQuality.Warning);
        }

        builder
            .FindingIf(best is not null && worst is not null && best.MarginalRoi > worst.MarginalRoi * 1.5,
                $"Предельная отдача каналов различается в {Fmt.Num((best?.MarginalRoi ?? 1) / Math.Max(worst?.MarginalRoi ?? 1, 1e-6))} раза. " +
                "Перенос бюджета из худшего канала в лучший увеличит продажи без роста затрат.")
            .FindingIf(saturated.Count > 0,
                $"Каналы у порога насыщения: {string.Join(", ", saturated.Select(c => c.Name))}. " +
                "Дополнительные вложения в них дают всё меньший прирост.")
            .FindingIf(Channels.Any(c => c.HalfLife > 3),
                $"Долгий след эффекта у канала «{Channels.OrderByDescending(c => c.HalfLife).First().Name}»: " +
                $"период полураспада {Fmt.Num(Channels.Max(c => c.HalfLife), 1)}. Оценивать его " +
                "по продажам той же недели нельзя.")
            .WarningIf(negative > 0,
                $"У {negative} каналов коэффициент отрицателен — модель утверждает, что реклама " +
                "снижает продажи. Обычно это следствие коллинеарности бюджетов: каналы включают " +
                "и выключают одновременно.")
            .WarningIf(RSquared > 0.97,
                "Подозрительно высокий R2: скорее всего модель переобучена на тренде и сезонности, " +
                "а вклад рекламы оценён неустойчиво.")
            .WarningIf(MediaShare > 0.6,
                "На рекламу отнесено больше половины продаж. Это редко бывает правдой: " +
                "проверьте, не забыта ли важная контрольная переменная (цена, дистрибуция).")
            .WarningIf(Fitted.Count < 60,
                $"Наблюдений всего {Fitted.Count}. Для устойчивой оценки adstock и насыщения " +
                "нужно минимум два года недельных данных.")
            .Warning("Модель корреляционная. Причинная интерпретация правомерна только при " +
                     "достаточной независимой вариации бюджетов — идеально при наличии " +
                     "географических экспериментов.")
            .RecommendationIf(unprofitable.Count > 0,
                $"Сократите бюджет каналов с предельной отдачей ниже единицы: " +
                $"{string.Join(", ", unprofitable.Select(c => c.Name))}.")
            .Recommendation("Перераспределяйте бюджет по предельному, а не среднему ROI: " +
                            "средний показатель включает уже сделанные вложения и всегда выше.")
            .Recommendation("Проверьте выводы географическим экспериментом на одном канале — " +
                            "это единственный способ отличить корреляцию от причины.");

        return builder.Build();
    }
}

/// <summary>
/// Маркетинг-микс модель: разложение продаж на вклад каналов с учётом
/// отложенного эффекта и насыщения.
/// </summary>
/// <remarks>
/// <para>
/// Две нелинейности отличают модель от обычной регрессии на затраты.
/// </para>
/// <para>
/// <b>Adstock</b> описывает отложенный эффект: реклама этой недели работает
/// и на следующей. Накопленный эффект <c>a_t = x_t + lambda * a_(t-1)</c>,
/// период полураспада равен <c>ln(0,5)/ln(lambda)</c>.
/// </para>
/// <para>
/// <b>Насыщение</b> по кривой Хилла <c>h(a) = a^alpha / (a^alpha + gamma^alpha)</c>
/// описывает убывающую отдачу: удвоение бюджета не удваивает эффект. Без него
/// модель предсказывает бесконечный рост продаж от бесконечного бюджета
/// и делает оптимизацию бессмысленной.
/// </para>
/// <para>
/// Параметры преобразований подбираются симплекс-методом, коэффициенты внутри
/// каждой итерации — гребневой регрессией. Регуляризация обязательна: бюджеты
/// каналов почти всегда движутся вместе.
/// </para>
/// </remarks>
public static class MarketingMixModel
{
    /// <summary>Оценивает модель.</summary>
    /// <param name="input">Продажи, каналы, контроли и настройки.</param>
    /// <returns>Декомпозиция продаж и показатели каналов.</returns>
    /// <exception cref="ArgumentNullException">Вход не задан.</exception>
    /// <exception cref="ArgumentException">Ряды разной длины или каналов нет.</exception>
    public static MmmResult Fit(MmmInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        int n = input.Sales.Count;
        if (n < 20) throw new ArgumentException("Нужно минимум 20 периодов наблюдений.", nameof(input));
        if (input.Channels.Count == 0) throw new ArgumentException("Нужен хотя бы один канал.", nameof(input));
        if (input.Channels.Any(c => c.Spend.Count != n))
            throw new ArgumentException("Длина ряда затрат должна совпадать с длиной ряда продаж.", nameof(input));

        int channels = input.Channels.Count;
        double[] y = [.. input.Sales];
        double[][] spend = [.. input.Channels.Select(c => c.Spend.ToArray())];
        double[] medians = [.. spend.Select(s => Median(s.Where(v => v > 0).ToArray()))];

        double[,] baseColumns = BuildBaseColumns(input, n);

        // Гиперпараметры в неограниченных координатах: логит для затухания,
        // логарифмы для формы и точки насыщения
        var start = new double[channels * 3];
        for (int c = 0; c < channels; c++)
        {
            start[c * 3] = 0.0;
            start[(c * 3) + 1] = 0.0;
            start[(c * 3) + 2] = 0.0;
        }

        double[] tuned = NelderMead.Minimize(
            p => Objective(p, spend, medians, baseColumns, y, input.Ridge),
            start, 0.4, input.TuningIterations);

        (double[] decay, double[] shape, double[] point) = Decode(tuned, medians);
        double[][] transformed = Transform(spend, decay, shape, point);

        double[,] design = Combine(transformed, baseColumns);
        OlsFit fit = Ols.Fit(design, y, input.Ridge)
            ?? throw new ArgumentException("Матрица регрессоров вырождена.", nameof(input));

        return BuildResult(input, fit, transformed, spend, decay, shape, point, baseColumns, y);
    }

    /// <summary>
    /// Накопленный эффект по геометрической схеме затухания.
    /// </summary>
    /// <param name="spend">Затраты по периодам.</param>
    /// <param name="decay">Коэффициент затухания из полуинтервала [0; 1).</param>
    /// <returns>Ряд накопленного эффекта.</returns>
    public static Vector Adstock(Vector spend, double decay)
    {
        ArgumentNullException.ThrowIfNull(spend);

        var result = new Vector(spend.Count);
        double carry = 0;

        for (int t = 0; t < spend.Count; t++)
        {
            carry = spend[t] + (decay * carry);
            result[t] = carry;
        }

        return result;
    }

    /// <summary>Кривая насыщения Хилла.</summary>
    /// <param name="value">Накопленный эффект.</param>
    /// <param name="halfPoint">Значение, дающее половину предельного эффекта.</param>
    /// <param name="shape">Крутизна кривой.</param>
    /// <returns>Отклик из отрезка [0; 1).</returns>
    public static double Hill(double value, double halfPoint, double shape)
    {
        if (value <= 0) return 0;
        double v = Math.Pow(value, shape);
        double g = Math.Pow(Math.Max(halfPoint, 1e-9), shape);
        return v / (v + g);
    }

    private static double Objective(
        double[] parameters, double[][] spend, double[] medians,
        double[,] baseColumns, double[] y, double ridge)
    {
        (double[] decay, double[] shape, double[] point) = Decode(parameters, medians);
        double[][] transformed = Transform(spend, decay, shape, point);
        double[,] design = Combine(transformed, baseColumns);

        OlsFit? fit = Ols.Fit(design, y, ridge);
        if (fit is null) return double.PositiveInfinity;

        double rss = 0;
        for (int i = 0; i < fit.Residuals.Length; i++) rss += fit.Residuals[i] * fit.Residuals[i];
        return rss;
    }

    /// <summary>Переводит неограниченные параметры в допустимые диапазоны.</summary>
    private static (double[] Decay, double[] Shape, double[] Point) Decode(double[] parameters, double[] medians)
    {
        int channels = medians.Length;
        var decay = new double[channels];
        var shape = new double[channels];
        var point = new double[channels];

        for (int c = 0; c < channels; c++)
        {
            // Затухание в [0; 0,9]: выше эффект тянется дольше квартала,
            // что для недельных данных почти всегда артефакт
            decay[c] = 0.9 / (1.0 + Math.Exp(-parameters[c * 3]));

            // Крутизна в [0,5; 3]: за пределами кривая либо линейна, либо ступенчата
            shape[c] = 0.5 + (2.5 / (1.0 + Math.Exp(-parameters[(c * 3) + 1])));

            // Точка насыщения привязана к медианным затратам канала
            double scale = Math.Exp(EconMath.Clamp(parameters[(c * 3) + 2], -2.5, 2.5));
            point[c] = Math.Max(medians[c], 1e-6) * scale / Math.Max(1.0 - decay[c], 0.1);
        }

        return (decay, shape, point);
    }

    private static double[][] Transform(double[][] spend, double[] decay, double[] shape, double[] point)
    {
        int channels = spend.Length;
        var transformed = new double[channels][];

        for (int c = 0; c < channels; c++)
        {
            int n = spend[c].Length;
            transformed[c] = new double[n];
            double carry = 0;

            for (int t = 0; t < n; t++)
            {
                carry = spend[c][t] + (decay[c] * carry);
                transformed[c][t] = Hill(carry, point[c], shape[c]);
            }
        }

        return transformed;
    }

    /// <summary>Столбцы базовой линии: свободный член, тренд, сезонность, контроли.</summary>
    private static double[,] BuildBaseColumns(MmmInput input, int n)
    {
        var columns = new List<double[]> { Enumerable.Repeat(1.0, n).ToArray() };

        if (input.IncludeTrend)
            columns.Add([.. Enumerable.Range(0, n).Select(t => (double)t / n)]);

        if (input.SeasonalPeriod > 1 && input.FourierTerms > 0)
        {
            for (int k = 1; k <= input.FourierTerms; k++)
            {
                int harmonic = k;
                columns.Add([.. Enumerable.Range(0, n)
                    .Select(t => Math.Sin(2 * Math.PI * harmonic * t / input.SeasonalPeriod))]);
                columns.Add([.. Enumerable.Range(0, n)
                    .Select(t => Math.Cos(2 * Math.PI * harmonic * t / input.SeasonalPeriod))]);
            }
        }

        foreach (MediaChannel control in input.Controls)
        {
            if (control.Spend.Count != n)
                throw new ArgumentException(
                    $"Длина контрольной переменной «{control.Name}» не совпадает с рядом продаж.");
            columns.Add([.. control.Spend]);
        }

        var matrix = new double[n, columns.Count];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < columns.Count; j++) matrix[i, j] = columns[j][i];

        return matrix;
    }

    private static double[,] Combine(double[][] media, double[,] baseColumns)
    {
        int n = baseColumns.GetLength(0);
        int baseCount = baseColumns.GetLength(1);
        var design = new double[n, media.Length + baseCount];

        for (int i = 0; i < n; i++)
        {
            for (int c = 0; c < media.Length; c++) design[i, c] = media[c][i];
            for (int j = 0; j < baseCount; j++) design[i, media.Length + j] = baseColumns[i, j];
        }

        return design;
    }

    private static MmmResult BuildResult(
        MmmInput input, OlsFit fit, double[][] transformed, double[][] spend,
        double[] decay, double[] shape, double[] point, double[,] baseColumns, double[] y)
    {
        int n = y.Length;
        int channels = spend.Length;
        double totalSales = y.Sum();

        var effects = new List<ChannelEffect>(channels);
        var baseline = new Vector(n);
        var fitted = new Vector(n);
        var residuals = new Vector(n);

        for (int t = 0; t < n; t++)
        {
            double media = 0;
            for (int c = 0; c < channels; c++) media += fit.Beta[c] * transformed[c][t];

            double basePart = 0;
            for (int j = 0; j < baseColumns.GetLength(1); j++)
                basePart += fit.Beta[channels + j] * baseColumns[t, j];

            baseline[t] = basePart;
            fitted[t] = media + basePart;
            residuals[t] = y[t] - fitted[t];
        }

        double mape = 0;
        int counted = 0;
        for (int t = 0; t < n; t++)
        {
            if (Math.Abs(y[t]) < 1e-9) continue;
            mape += Math.Abs(residuals[t] / y[t]);
            counted++;
        }
        mape = counted > 0 ? mape / counted : double.NaN;

        for (int c = 0; c < channels; c++)
        {
            var contribution = new Vector(n);
            double total = 0;
            for (int t = 0; t < n; t++)
            {
                contribution[t] = fit.Beta[c] * transformed[c][t];
                total += contribution[t];
            }

            double channelSpend = spend[c].Sum();
            double marginal = MarginalReturn(spend[c], decay[c], shape[c], point[c], fit.Beta[c], input.MarginRate);
            double steadyState = spend[c].Average() / Math.Max(1.0 - decay[c], 1e-6);

            effects.Add(new ChannelEffect
            {
                Name = input.Channels[c].Name,
                Decay = decay[c],
                SaturationPoint = point[c],
                SaturationShape = shape[c],
                Coefficient = fit.Beta[c],
                TotalContribution = total,
                ContributionShare = totalSales > 0 ? total / totalSales : 0,
                TotalSpend = channelSpend,
                Roi = channelSpend > 0 ? total * input.MarginRate / channelSpend : double.NaN,
                MarginalRoi = marginal,
                SaturationLevel = point[c] > 0 ? steadyState / point[c] : 0,
                Contribution = contribution,
            });
        }

        double mediaTotal = effects.Sum(e => e.TotalContribution);

        return new MmmResult
        {
            Channels = [.. effects.OrderByDescending(e => e.TotalContribution)],
            BaselineContribution = baseline.Sum(),
            TotalSales = totalSales,
            MediaShare = totalSales > 0 ? mediaTotal / totalSales : 0,
            RSquared = fit.RSquared,
            Mape = mape,
            Fitted = fitted,
            Baseline = baseline,
            Residuals = residuals,
            MarginRate = input.MarginRate,
        };
    }

    /// <summary>
    /// Предельная отдача: прирост прибыли на дополнительный рубль при текущем
    /// уровне затрат. Считается численно по всему ряду, чтобы учесть и
    /// накопление, и насыщение.
    /// </summary>
    private static double MarginalReturn(
        double[] spend, double decay, double shape, double point, double beta, double marginRate)
    {
        double total = spend.Sum();
        if (total <= 0) return double.NaN;

        double delta = Math.Max(total * 0.01, 1e-6);
        double factor = 1.0 + (delta / total);

        double baseResponse = 0, bumpedResponse = 0;
        double carry = 0, carryBumped = 0;

        for (int t = 0; t < spend.Length; t++)
        {
            carry = spend[t] + (decay * carry);
            carryBumped = (spend[t] * factor) + (decay * carryBumped);
            baseResponse += Hill(carry, point, shape);
            bumpedResponse += Hill(carryBumped, point, shape);
        }

        return beta * (bumpedResponse - baseResponse) * marginRate / delta;
    }

    private static double Median(double[] values)
    {
        if (values.Length == 0) return 1.0;
        double[] sorted = [.. values.OrderBy(v => v)];
        return EconMath.Quantile(sorted, 0.5);
    }
}
