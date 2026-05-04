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
            _logApriori[i] = StatUtils.SafeLog(_apriori[i]);
        }
    }

    /// <summary>Нормированный апрайер.</summary>
    public Vector Apriori => _apriori;

    #region argmax индикатор

    /// <summary>Индекс класса с максимальным log-апостериором (1D).</summary>
    public int LogArgmax1D(double inp)
    {
        int k = _distributions.Length;
        int best = 0;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            double s = _logApriori[i] + _distributions[i].CulcLogProb(inp);
            if (s > bestScore) { bestScore = s; best = i; }
        }
        return best;
    }

    /// <summary>Индекс класса с максимальным log-апостериором (ND).</summary>
    public int LogArgmaxND(Vector inp)
    {
        int k = _distributions.Length;
        int best = 0;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            double s = _logApriori[i] + _distributions[i].CulcLogProb(inp);
            if (s > bestScore) { bestScore = s; best = i; }
        }
        return best;
    }

    #endregion

    #region нормированные апостериорные вероятности

    /// <summary>
    /// Нормированный апостериор P(k | x) для 1D (log-softmax).
    /// </summary>
    public Vector Posterior(double inp)
    {
        int k = _distributions.Length;
        double[] logs = new double[k];
        for (int i = 0; i < k; i++)
            logs[i] = _logApriori[i] + _distributions[i].CulcLogProb(inp);

        double[] target = new double[k];
        StatUtils.LogSoftmax(logs, target);
        return new Vector(target);
    }

    /// <summary>
    /// Нормированный апостериор P(k | x) для ND.
    /// </summary>
    public Vector Posterior(Vector inp)
    {
        int k = _distributions.Length;
        double[] logs = new double[k];
        for (int i = 0; i < k; i++)
            logs[i] = _logApriori[i] + _distributions[i].CulcLogProb(inp);

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
    /// </summary>
    public static Vector CalcAposteori(Vector conditionalProbabilities, Vector apriori)
    {
        if (conditionalProbabilities.Count != apriori.Count)
            throw new ArgumentException("Длины векторов должны совпадать");

        Vector joint = new Vector(apriori.Count);
        double total = 0;
        for (int i = 0; i < joint.Count; i++)
        {
            joint[i] = conditionalProbabilities[i] * apriori[i];
            total += joint[i];
        }
        if (total <= 0) total = AISettings.GlobalEps;
        for (int i = 0; i < joint.Count; i++) joint[i] /= total;
        return joint;
    }

    /// <summary>Argmax log-апостериора, 1D, параметризованная версия.</summary>
    public static int LogArgmax1D(
        double inp, IDistribution distribution,
        Dictionary<string, double>[] param_dist, Vector apriori)
    {
        int k = apriori.Count;
        int best = 0;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            double s = StatUtils.SafeLog(apriori[i]) + distribution.CulcLogProb(inp, param_dist[i]);
            if (s > bestScore) { bestScore = s; best = i; }
        }
        return best;
    }

    /// <summary>Argmax log-апостериора, ND, параметризованная версия.</summary>
    public static int LogArgmaxND(
        Vector inp, IDistribution distribution,
        Dictionary<string, Vector>[] param_dist, Vector apriori)
    {
        int k = apriori.Count;
        int best = 0;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            double s = StatUtils.SafeLog(apriori[i]) + distribution.CulcLogProb(inp, param_dist[i]);
            if (s > bestScore) { bestScore = s; best = i; }
        }
        return best;
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
