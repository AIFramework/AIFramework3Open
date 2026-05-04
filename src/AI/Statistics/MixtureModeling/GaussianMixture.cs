using AI.DataStructs.Algebraic;
using AI.Statistics.Distributions;
using System;
using System.Collections.Generic;

namespace AI.Statistics.MixtureModeling;

/// <summary>
/// Гауссова смесь с диагональной ковариацией (одна реализация для
/// 1D и ND-случаев: 1D — это частный случай ND с Dim = 1).
/// 
/// Сама является <see cref="IDistributionWithoutParams"/> и
/// <see cref="ISamplableDistribution"/> — можно вкладывать в другие
/// смеси, байесовский вывод, MCMC.
/// </summary>
/// <remarks>
/// Все плотности вычисляются в лог-пространстве через log-sum-exp;
/// апостериорные вероятности считаются stable-softmax'ом. Минимальный
/// возможный σ ограничивается снизу параметром <see cref="MinStd"/>
/// (защита от вырождения компонент на EM).
/// </remarks>
[Serializable]
public sealed class GaussianMixture : IDistributionWithoutParams, ISamplableDistribution
{
    #region Состояние

    /// <summary>Число компонент.</summary>
    public int K { get; }

    /// <summary>Размерность (Dim = 1 -> 1D-смесь).</summary>
    public int Dim { get; }

    /// <summary>Веса компонент (сумма = 1).</summary>
    public Vector Weights { get; }

    /// <summary>Средние компонент (K × Dim).</summary>
    public Vector[] Means { get; }

    /// <summary>Диагональные СКО компонент (K × Dim).</summary>
    public Vector[] Stds { get; }

    /// <summary>
    /// Логарифмическое правдоподобие на обучающей выборке
    /// (заполняется EM-фитом; −∞, если модель построена вручную).
    /// </summary>
    public double LogLikelihood { get; internal set; } = double.NegativeInfinity;

    /// <summary>
    /// Нижняя граница СКО — защита от вырождения компонент в
    /// дельта-функции на EM (одна точка ⇒ σ -> 0 ⇒ log p -> +∞).
    /// </summary>
    public double MinStd { get; set; } = 1e-6;

    /// <inheritdoc/>
    public bool IsOneDimensional => Dim == 1;

    /// <summary>Алиас для совместимости с MixtureModel.</summary>
    public bool IsOneD => Dim == 1;

    #endregion

    #region Constructors

    /// <summary>
    /// Построение смеси по готовым параметрам. Веса будут
    /// отнормированы.
    /// </summary>
    public GaussianMixture(Vector weights, Vector[] means, Vector[] stds)
    {
        if (weights == null || means == null || stds == null)
            throw new ArgumentNullException();
        if (weights.Count != means.Length || means.Length != stds.Length)
            throw new ArgumentException("Размеры weights/means/stds не согласованы");

        K = weights.Count;
        if (K == 0) throw new ArgumentException("Пустой набор компонент");
        Dim = means[0].Count;

        for (int k = 0; k < K; k++)
        {
            if (means[k].Count != Dim || stds[k].Count != Dim)
                throw new ArgumentException($"Размерность компоненты {k} не согласована");
        }

        Weights = new Vector(K);
        double total = 0;
        for (int k = 0; k < K; k++)
        {
            if (weights[k] < 0) throw new ArgumentException("Вес < 0");
            total += weights[k];
        }
        if (total <= 0) throw new ArgumentException("Сумма весов равна 0");
        for (int k = 0; k < K; k++) Weights[k] = weights[k] / total;

        Means = new Vector[K];
        Stds = new Vector[K];
        for (int k = 0; k < K; k++)
        {
            Means[k] = means[k].Clone();
            Stds[k] = stds[k].Clone();
        }
    }

    /// <summary>Удобный конструктор для 1D: массивы скаляров.</summary>
    public static GaussianMixture From1D(double[] weights, double[] means, double[] stds)
    {
        int k = weights.Length;
        var w = new Vector(weights);
        var m = new Vector[k];
        var s = new Vector[k];
        for (int i = 0; i < k; i++)
        {
            m[i] = new Vector(means[i]);
            s[i] = new Vector(stds[i]);
        }
        return new GaussianMixture(w, m, s);
    }

    #endregion

    #region Плотности / Лог-плотности

    /// <summary>Лог-плотность N(x | μ_k, diag(σ_k)).</summary>
    public double ComponentLogProb(int k, Vector x)
    {
        Vector mean = Means[k], std = Stds[k];
        // 0.5 log(2π) * Dim
        double logConst = 0.5 * Dim * Math.Log(2.0 * Math.PI);
        double sumLogStd = 0.0;
        double sumZ2 = 0.0;
        for (int d = 0; d < Dim; d++)
        {
            double s = Math.Max(std[d], MinStd);
            sumLogStd += Math.Log(s);
            double z = (x[d] - mean[d]) / s;
            sumZ2 += z * z;
        }
        return (-0.5 * sumZ2) - sumLogStd - logConst;
    }

    /// <summary>1D-вариант без аллокации Vector.</summary>
    public double ComponentLogProb(int k, double x)
    {
        double mean = Means[k][0];
        double s = Math.Max(Stds[k][0], MinStd);
        double z = (x - mean) / s;
        return (-0.5 * z * z) - Math.Log(s) - (0.5 * Math.Log(2.0 * Math.PI));
    }

    /// <inheritdoc/>
    public double CulcLogProb(Vector x)
    {
        Span<double> logs = K <= 64 ? stackalloc double[K] : new double[K];
        for (int k = 0; k < K; k++)
            logs[k] = StatUtils.SafeLog(Weights[k]) + ComponentLogProb(k, x);
        return StatUtils.LogSumExp(logs);
    }

    /// <inheritdoc/>
    public double CulcLogProb(double x)
    {
        if (!IsOneD) throw new InvalidOperationException("Это ND-смесь");
        Span<double> logs = K <= 64 ? stackalloc double[K] : new double[K];
        for (int k = 0; k < K; k++)
            logs[k] = StatUtils.SafeLog(Weights[k]) + ComponentLogProb(k, x);
        return StatUtils.LogSumExp(logs);
    }

    /// <inheritdoc/>
    public double CulcProb(Vector x) => Math.Exp(CulcLogProb(x));

    /// <inheritdoc/>
    public double CulcProb(double x) => Math.Exp(CulcLogProb(x));

    /// <summary>Апостериорные вероятности компонент для x.</summary>
    public Vector Posterior(Vector x)
    {
        double[] logs = new double[K];
        for (int k = 0; k < K; k++)
            logs[k] = StatUtils.SafeLog(Weights[k]) + ComponentLogProb(k, x);

        double[] target = new double[K];
        StatUtils.LogSoftmax(logs, target);
        return new Vector(target);
    }

    /// <summary>Индекс компоненты с максимальной апостериорной.</summary>
    public int Argmax(Vector x) => Posterior(x).MaxElementIndex();

    #endregion

    #region Сэмплирование

    /// <inheritdoc/>
    public double Sample1D(Random rng)
    {
        if (!IsOneD) throw new NotSupportedException("ND-смесь");
        int k = RandomItemSelection.GetIndex(Weights, rng);
        return NonCorrelatedGaussian.Sample(Means[k][0], Stds[k][0], rng);
    }

    /// <inheritdoc/>
    public Vector SampleND(Random rng)
    {
        if (IsOneD) throw new NotSupportedException("1D-смесь");
        int k = RandomItemSelection.GetIndex(Weights, rng);
        return NonCorrelatedGaussian.Sample(Means[k], Stds[k], rng);
    }

    #endregion

    #region Информационные критерии

    /// <summary>
    /// Количество свободных параметров модели: (K − 1) веса +
    /// K × Dim средних + K × Dim СКО.
    /// </summary>
    public int NumFreeParameters => (K - 1) + (2 * K * Dim);

    /// <summary>
    /// Bayesian Information Criterion. Требует заполненного
    /// <see cref="LogLikelihood"/>.
    /// </summary>
    public double Bic(int sampleCount)
        => (-2.0 * LogLikelihood) + (NumFreeParameters * Math.Log(sampleCount));

    /// <summary>
    /// Akaike Information Criterion. Требует заполненного
    /// <see cref="LogLikelihood"/>.
    /// </summary>
    public double Aic => (-2.0 * LogLikelihood) + (2.0 * NumFreeParameters);

    #endregion

    #region Мост к MixtureModel

    /// <summary>
    /// Превращает эту смесь в универсальную <see cref="MixtureModel"/>
    /// с компонентами <see cref="NonCorrelatedGaussian"/>. Полезно
    /// для передачи в код, ожидающий MixtureModel.
    /// </summary>
    public MixtureModel ToMixtureModel()
    {
        var parent = new NonCorrelatedGaussian();
        if (IsOneD)
        {
            var paramList = new Dictionary<string, double>[K];
            for (int k = 0; k < K; k++)
            {
                paramList[k] = new Dictionary<string, double>
                {
                    [NonCorrelatedGaussian.KeyMean] = Means[k][0],
                    [NonCorrelatedGaussian.KeyStd] = Math.Max(Stds[k][0], MinStd),
                };
            }
            return new MixtureModel(parent, paramList, Weights.Clone());
        }
        else
        {
            var paramList = new Dictionary<string, Vector>[K];
            for (int k = 0; k < K; k++)
            {
                paramList[k] = new Dictionary<string, Vector>
                {
                    [NonCorrelatedGaussian.KeyMean] = Means[k].Clone(),
                    [NonCorrelatedGaussian.KeyStd] = Stds[k].Transform(s => Math.Max(s, MinStd)),
                };
            }
            return new MixtureModel(parent, paramList, Weights.Clone());
        }
    }

    #endregion
}
