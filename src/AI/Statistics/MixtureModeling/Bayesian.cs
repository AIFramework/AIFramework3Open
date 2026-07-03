using AI.DataStructs.Algebraic;
using AI.Statistics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AI.Statistics.MixtureModeling;

/// <summary>
/// Инструменты байесовского вывода на дискретной смеси классов.
/// 
/// Важное отличие от старой версии: <b>log-апостериор</b> строится
/// как сумма <c>log π_k + log p(x | k)</c>, а не как произведение
/// лог-плотности на вес. Это математически корректно и
/// численно устойчиво (log-softmax для апостериорных).
/// </summary>
[Serializable]
public class Bayesian
{
    private readonly IDistributionWithoutParams[] _distributions;
    private readonly Vector _apriori;
    private readonly double[] _logApriori;

    /// <summary>
    /// Создаёт байесовский оракул из списка распределений и
    /// априорных вероятностей классов.
    /// </summary>
    public Bayesian(IEnumerable<IDistributionWithoutParams> distributions, Vector apriori)
    {
        if (distributions == null) throw new ArgumentNullException(nameof(distributions));
        if (apriori == null) throw new ArgumentNullException(nameof(apriori));

        _distributions = distributions.ToArray();

        if (_distributions.Length != apriori.Count)
            throw new ArgumentException(
                "Число распределений и апрайеров должно совпадать");

        // нормируем апрайер
        double total = 0;
        for (int i = 0; i < apriori.Count; i++)
        {
            if (apriori[i] < 0) throw new ArgumentException("Апрайер < 0");
            total += apriori[i];
        }
        if (total <= 0) throw new ArgumentException("Сумма апрайеров = 0");

        _apriori = new Vector(apriori.Count);
        _logApriori = new double[apriori.Count];
        for (int i = 0; i < apriori.Count; i++)
        {
            _apriori[i] = apriori[i] / total;
            // Нулевой апрайер должен давать log = -∞ (класс полностью
            // исключён), а не конечный «пол» SafeLog, который сильное
            // правдоподобие могло бы перекрыть в argmax.
            _logApriori[i] = _apriori[i] > 0
                ? Math.Log(_apriori[i])
                : double.NegativeInfinity;
        }
    }

    /// <summary>Нормированный апрайер.</summary>
    public Vector Apriori => _apriori;

    #region argmax индикатор

    /// <summary>
    /// Индекс класса с максимальным log-апостериором (1D).
    /// Классы с нулевым апрайером исключаются из argmax; если у всех
    /// классов log-апостериор равен -∞ (все плотности нулевые),
    /// возвращается argmax апрайера (fallback без свидетельства).
    /// </summary>
    public int LogArgmax1D(double inp)
    {
        int k = _distributions.Length;
        int best = -1;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            // Нулевой апрайер => класс исключён, плотность не считаем
            if (double.IsNegativeInfinity(_logApriori[i])) continue;
            double s = _logApriori[i] + _distributions[i].CulcLogProb(inp);
            if (s > bestScore) { bestScore = s; best = i; }
        }
        return best >= 0 ? best : _apriori.MaxElementIndex();
    }

    /// <summary>
    /// Индекс класса с максимальным log-апостериором (ND).
    /// Классы с нулевым апрайером исключаются из argmax; если у всех
    /// классов log-апостериор равен -∞ (все плотности нулевые),
    /// возвращается argmax апрайера (fallback без свидетельства).
    /// </summary>
    public int LogArgmaxND(Vector inp)
    {
        int k = _distributions.Length;
        int best = -1;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            // Нулевой апрайер => класс исключён, плотность не считаем
            if (double.IsNegativeInfinity(_logApriori[i])) continue;
            double s = _logApriori[i] + _distributions[i].CulcLogProb(inp);
            if (s > bestScore) { bestScore = s; best = i; }
        }
        return best >= 0 ? best : _apriori.MaxElementIndex();
    }

    #endregion

    #region нормированные апостериорные вероятности

    /// <summary>
    /// Нормированный апостериор P(k | x) для 1D (log-softmax).
    /// Классы с нулевым апрайером получают апостериор строго 0.
    /// Если все log-апостериоры равны -∞ (все плотности нулевые),
    /// свидетельство отсутствует — возвращается нормированный апрайер.
    /// </summary>
    public Vector Posterior(double inp)
    {
        int k = _distributions.Length;
        double[] logs = new double[k];
        bool anyFinite = false;
        for (int i = 0; i < k; i++)
        {
            // Нулевой апрайер => апостериор строго 0 (плотность не считаем)
            logs[i] = double.IsNegativeInfinity(_logApriori[i])
                ? double.NegativeInfinity
                : _logApriori[i] + _distributions[i].CulcLogProb(inp);
            if (!double.IsNegativeInfinity(logs[i])) anyFinite = true;
        }

        // Все плотности нулевые: fallback без свидетельства — апрайер
        if (!anyFinite) return _apriori.Clone();

        double[] target = new double[k];
        StatUtils.LogSoftmax(logs, target);
        return new Vector(target);
    }

    /// <summary>
    /// Нормированный апостериор P(k | x) для ND.
    /// Классы с нулевым апрайером получают апостериор строго 0.
    /// Если все log-апостериоры равны -∞ (все плотности нулевые),
    /// свидетельство отсутствует — возвращается нормированный апрайер.
    /// </summary>
    public Vector Posterior(Vector inp)
    {
        int k = _distributions.Length;
        double[] logs = new double[k];
        bool anyFinite = false;
        for (int i = 0; i < k; i++)
        {
            // Нулевой апрайер => апостериор строго 0 (плотность не считаем)
            logs[i] = double.IsNegativeInfinity(_logApriori[i])
                ? double.NegativeInfinity
                : _logApriori[i] + _distributions[i].CulcLogProb(inp);
            if (!double.IsNegativeInfinity(logs[i])) anyFinite = true;
        }

        // Все плотности нулевые: fallback без свидетельства — апрайер
        if (!anyFinite) return _apriori.Clone();

        double[] target = new double[k];
        StatUtils.LogSoftmax(logs, target);
        return new Vector(target);
    }

    #endregion

    #region Пакетные операции (параллельные)

    /// <summary>Индикаторы для 1D-выборки (параллельная обработка).</summary>
    public int[] GetIndicators(Vector inps)
    {
        int[] result = new int[inps.Count];
        Parallel.For(0, inps.Count, i => result[i] = LogArgmax1D(inps[i]));
        return result;
    }

    /// <summary>Индикаторы для ND-выборки (параллельная обработка).</summary>
    public int[] GetIndicators(Vector[] inps)
    {
        int[] result = new int[inps.Length];
        Parallel.For(0, inps.Length, i => result[i] = LogArgmaxND(inps[i]));
        return result;
    }

    #endregion

    #region Статический API с параметризованными распределениями

    /// <summary>
    /// Апостериор по Байесу: (conditional × apriori) нормировано.
    /// Оставлен для обратной совместимости.
    ///
    /// Расчёт идёт в лог-пространстве: log p_i = log(apriori_i) +
    /// log(conditional_i), затем вычитается максимум и берётся softmax.
    /// Это защищает от underflow, когда все произведения
    /// prior × likelihood меньше ~1e-308 (раньше в этом случае
    /// возвращался нулевой, ненормированный «апостериор»).
    ///
    /// Нулевой (или отрицательный) апрайер даёт апостериор строго 0.
    /// Если все log-апостериоры равны -∞ (все плотности нулевые),
    /// свидетельство отсутствует — возвращается нормированный апрайер;
    /// в вырожденном случае нулевой суммы апрайеров — равномерное
    /// распределение.
    /// </summary>
    public static Vector CalcAposteori(Vector conditionalProbabilities, Vector apriori)
    {
        if (conditionalProbabilities.Count != apriori.Count)
            throw new ArgumentException("Длины векторов должны совпадать");

        int n = apriori.Count;
        double[] logs = new double[n];
        double max = double.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            // Нулевой апрайер или нулевая плотность => log = -∞
            logs[i] = apriori[i] > 0 && conditionalProbabilities[i] > 0
                ? Math.Log(apriori[i]) + Math.Log(conditionalProbabilities[i])
                : double.NegativeInfinity;
            if (logs[i] > max) max = logs[i];
        }

        Vector posterior = new Vector(n);

        if (double.IsNegativeInfinity(max))
        {
            // Все плотности нулевые — возвращаем нормированный апрайер
            double aprioriTotal = 0;
            for (int i = 0; i < n; i++)
                if (apriori[i] > 0) aprioriTotal += apriori[i];

            if (aprioriTotal <= 0)
            {
                // Вырожденный вход: апрайер весь нулевой — равномерно
                for (int i = 0; i < n; i++) posterior[i] = 1.0 / n;
                return posterior;
            }

            for (int i = 0; i < n; i++)
                posterior[i] = apriori[i] > 0 ? apriori[i] / aprioriTotal : 0.0;
            return posterior;
        }

        // Устойчивый softmax: вычитаем максимум перед экспонентой
        double total = 0;
        for (int i = 0; i < n; i++)
        {
            posterior[i] = Math.Exp(logs[i] - max);
            total += posterior[i];
        }
        for (int i = 0; i < n; i++) posterior[i] /= total;
        return posterior;
    }

    /// <summary>
    /// Argmax log-апостериора, 1D, параметризованная версия.
    /// Классы с нулевым апрайером исключаются из argmax; если все
    /// log-апостериоры равны -∞, возвращается argmax апрайера.
    /// </summary>
    public static int LogArgmax1D(
        double inp, IDistribution distribution,
        Dictionary<string, double>[] param_dist, Vector apriori)
    {
        int k = apriori.Count;
        int best = -1;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            // Нулевой апрайер => класс исключён (log(0) = -∞, а не «пол» SafeLog)
            if (apriori[i] <= 0) continue;
            double s = Math.Log(apriori[i]) + distribution.CulcLogProb(inp, param_dist[i]);
            if (s > bestScore) { bestScore = s; best = i; }
        }
        return best >= 0 ? best : apriori.MaxElementIndex();
    }

    /// <summary>
    /// Argmax log-апостериора, ND, параметризованная версия.
    /// Классы с нулевым апрайером исключаются из argmax; если все
    /// log-апостериоры равны -∞, возвращается argmax апрайера.
    /// </summary>
    public static int LogArgmaxND(
        Vector inp, IDistribution distribution,
        Dictionary<string, Vector>[] param_dist, Vector apriori)
    {
        int k = apriori.Count;
        int best = -1;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            // Нулевой апрайер => класс исключён (log(0) = -∞, а не «пол» SafeLog)
            if (apriori[i] <= 0) continue;
            double s = Math.Log(apriori[i]) + distribution.CulcLogProb(inp, param_dist[i]);
            if (s > bestScore) { bestScore = s; best = i; }
        }
        return best >= 0 ? best : apriori.MaxElementIndex();
    }

    /// <summary>Пакет 1D-индикаторов, параметризованная версия (параллельно).</summary>
    public static int[] GetIndicators(
        Vector inps, IDistribution distribution,
        Dictionary<string, double>[] param_dist, Vector apriori)
    {
        int[] result = new int[inps.Count];
        Parallel.For(0, inps.Count,
            i => result[i] = LogArgmax1D(inps[i], distribution, param_dist, apriori));
        return result;
    }

    /// <summary>Пакет ND-индикаторов, параметризованная версия (параллельно).</summary>
    public static int[] GetIndicators(
        Vector[] inps, IDistribution distribution,
        Dictionary<string, Vector>[] param_dist, Vector apriori)
    {
        int[] result = new int[inps.Length];
        Parallel.For(0, inps.Length,
            i => result[i] = LogArgmaxND(inps[i], distribution, param_dist, apriori));
        return result;
    }

    #endregion
}
