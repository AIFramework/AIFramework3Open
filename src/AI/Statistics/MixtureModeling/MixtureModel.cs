using AI.DataStructs.Algebraic;
using AI.Statistics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Statistics.MixtureModeling;

/// <summary>
/// Смесь вероятностных распределений.
/// 
/// Логика «1D vs ND» определяется компонентами: смесь сама
/// <see cref="IDistributionWithoutParams"/> и может быть
/// элементом другой смеси (композиция рекурсивна).
/// 
/// Все вычисления плотности и лог-плотности идут через
/// численно устойчивый log-sum-exp, апостериорные вероятности
/// (responsibilities) считаются в лог-пространстве с последующей
/// softmax-нормировкой — это избавляет от типичных underflow при
/// высокой размерности.
/// </summary>
[Serializable]
public class MixtureModel : IDistributionWithoutParams, ISamplableDistribution
{
    #region Поля

    private readonly IDistributionWithoutParams[] _components;
    private readonly double[] _logWeights; // лог-веса (ln(w) где Σw = 1); вес 0 => -∞
    private readonly Vector _weights;
    private readonly bool _isOneD;

    #endregion

    #region Публичные свойства

    /// <summary>Компоненты смеси.</summary>
    public IReadOnlyList<IDistributionWithoutParams> Components => _components;

    /// <summary>Нормированные веса компонент (суммируются в 1).</summary>
    public Vector Weights => _weights;

    /// <summary>true, если смесь одномерна.</summary>
    public bool IsOneD => _isOneD;

    /// <inheritdoc/>
    public bool IsOneDimensional => _isOneD;

    #endregion

    #region Конструкторы

    /// <summary>
    /// Новый универсальный конструктор: любые компоненты +
    /// нормированные веса. Размерность определяется по первой
    /// компоненте.
    /// </summary>
    public MixtureModel(IReadOnlyList<IDistributionWithoutParams> components, Vector weights)
    {
        if (components == null || components.Count == 0)
            throw new ArgumentException("Список компонент пуст", nameof(components));
        if (weights == null || weights.Count != components.Count)
            throw new ArgumentException(
                "Длины weights и components должны совпадать", nameof(weights));

        _components = components.ToArray();
        _weights = NormalizeWeights(weights);
        _logWeights = new double[_components.Length];
        for (int i = 0; i < _components.Length; i++)
            // Нулевой вес должен давать log = -∞ (компонента полностью
            // исключена), а не конечный «пол» SafeLog, который большая
            // плотность могла бы перекрыть в Posterior/Argmax.
            _logWeights[i] = _weights[i] > 0
                ? Math.Log(_weights[i])
                : double.NegativeInfinity;

        _isOneD = DetermineOneD(_components);
    }

    /// <summary>
    /// Легаси-конструктор: parent + массив параметров 1D-распределений
    /// + веса. Внутренне превращается в массив
    /// <see cref="BoundDistribution1D"/>.
    /// </summary>
    public MixtureModel(
        IDistribution perentDistribution,
        IEnumerable<Dictionary<string, double>> paramDists,
        Vector w)
        : this(BuildBound1D(perentDistribution, paramDists), w)
    {
    }

    /// <summary>
    /// Легаси-конструктор: parent + массив параметров ND-распределений
    /// + веса.
    /// </summary>
    public MixtureModel(
        IDistribution perentDistribution,
        IEnumerable<Dictionary<string, Vector>> paramDists,
        Vector w)
        : this(BuildBoundND(perentDistribution, paramDists), w)
    {
    }

    private static IDistributionWithoutParams[] BuildBound1D(
        IDistribution parent, IEnumerable<Dictionary<string, double>> paramDists)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        var list = paramDists?.ToArray()
                   ?? throw new ArgumentNullException(nameof(paramDists));
        var arr = new IDistributionWithoutParams[list.Length];
        for (int i = 0; i < list.Length; i++)
            arr[i] = new BoundDistribution1D(parent, list[i]);
        return arr;
    }

    private static IDistributionWithoutParams[] BuildBoundND(
        IDistribution parent, IEnumerable<Dictionary<string, Vector>> paramDists)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        var list = paramDists?.ToArray()
                   ?? throw new ArgumentNullException(nameof(paramDists));
        var arr = new IDistributionWithoutParams[list.Length];
        for (int i = 0; i < list.Length; i++)
            arr[i] = new BoundDistributionND(parent, list[i]);
        return arr;
    }

    #endregion

    #region Плотности и лог-плотности

    /// <inheritdoc/>
    public double CulcLogProb(double x)
    {
        EnsureOneD();
        Span<double> logs = _components.Length <= 64
            ? stackalloc double[_components.Length]
            : new double[_components.Length];

        for (int i = 0; i < _components.Length; i++)
            // Компонента с нулевым весом не вносит вклада (плотность не считаем)
            logs[i] = double.IsNegativeInfinity(_logWeights[i])
                ? double.NegativeInfinity
                : _logWeights[i] + _components[i].CulcLogProb(x);

        return StatUtils.LogSumExp(logs);
    }

    /// <inheritdoc/>
    public double CulcLogProb(Vector x)
    {
        EnsureND();
        Span<double> logs = _components.Length <= 64
            ? stackalloc double[_components.Length]
            : new double[_components.Length];

        for (int i = 0; i < _components.Length; i++)
            // Компонента с нулевым весом не вносит вклада (плотность не считаем)
            logs[i] = double.IsNegativeInfinity(_logWeights[i])
                ? double.NegativeInfinity
                : _logWeights[i] + _components[i].CulcLogProb(x);

        return StatUtils.LogSumExp(logs);
    }

    /// <inheritdoc/>
    public double CulcProb(double x) => Math.Exp(CulcLogProb(x));
    /// <inheritdoc/>
    public double CulcProb(Vector x) => Math.Exp(CulcLogProb(x));

    #endregion

    #region Апостериорные вероятности (responsibilities)

    /// <summary>
    /// Возвращает нормированные апостериорные вероятности
    /// компонент для одномерного наблюдения <paramref name="x"/>.
    /// Расчёт идёт в лог-пространстве (stable softmax).
    /// Компоненты с нулевым весом получают апостериор строго 0.
    /// Если все log-апостериоры равны -∞ (все плотности нулевые),
    /// свидетельство отсутствует — возвращаются нормированные веса.
    /// </summary>
    public Vector Posterior(double x)
    {
        EnsureOneD();
        double[] logs = new double[_components.Length];
        bool anyFinite = false;
        for (int i = 0; i < _components.Length; i++)
        {
            // Нулевой вес => апостериор строго 0 (плотность не считаем)
            logs[i] = double.IsNegativeInfinity(_logWeights[i])
                ? double.NegativeInfinity
                : _logWeights[i] + _components[i].CulcLogProb(x);
            if (!double.IsNegativeInfinity(logs[i])) anyFinite = true;
        }

        // Все плотности нулевые: fallback без свидетельства — веса смеси
        if (!anyFinite) return _weights.Clone();

        double[] target = new double[logs.Length];
        StatUtils.LogSoftmax(logs, target);
        return new Vector(target);
    }

    /// <summary>
    /// Апостериорные вероятности компонент для многомерного x.
    /// Компоненты с нулевым весом получают апостериор строго 0.
    /// Если все log-апостериоры равны -∞ (все плотности нулевые),
    /// свидетельство отсутствует — возвращаются нормированные веса.
    /// </summary>
    public Vector Posterior(Vector x)
    {
        EnsureND();
        double[] logs = new double[_components.Length];
        bool anyFinite = false;
        for (int i = 0; i < _components.Length; i++)
        {
            // Нулевой вес => апостериор строго 0 (плотность не считаем)
            logs[i] = double.IsNegativeInfinity(_logWeights[i])
                ? double.NegativeInfinity
                : _logWeights[i] + _components[i].CulcLogProb(x);
            if (!double.IsNegativeInfinity(logs[i])) anyFinite = true;
        }

        // Все плотности нулевые: fallback без свидетельства — веса смеси
        if (!anyFinite) return _weights.Clone();

        double[] target = new double[logs.Length];
        StatUtils.LogSoftmax(logs, target);
        return new Vector(target);
    }

    /// <summary>
    /// Индекс наиболее вероятной компоненты (argmax апостериорной).
    /// </summary>
    public int Argmax(double x) => Posterior(x).MaxElementIndex();

    /// <summary>
    /// Индекс наиболее вероятной компоненты для многомерного x.
    /// </summary>
    public int Argmax(Vector x) => Posterior(x).MaxElementIndex();

    #endregion

    #region Сэмплирование

    /// <inheritdoc/>
    public double Sample1D(Random rng)
    {
        EnsureOneD();
        int k = RandomItemSelection.GetIndex(_weights, rng);
        if (_components[k] is ISamplableDistribution s && s.IsOneDimensional)
            return s.Sample1D(rng);

        throw new NotSupportedException(
            $"Компонента {_components[k].GetType().Name} не поддерживает сэмплирование.");
    }

    /// <inheritdoc/>
    public Vector SampleND(Random rng)
    {
        EnsureND();
        int k = RandomItemSelection.GetIndex(_weights, rng);
        if (_components[k] is ISamplableDistribution s && !s.IsOneDimensional)
            return s.SampleND(rng);

        throw new NotSupportedException(
            $"Компонента {_components[k].GetType().Name} не поддерживает сэмплирование.");
    }

    #endregion

    #region Вспомогательные

    private static Vector NormalizeWeights(Vector w)
    {
        double total = 0.0;
        for (int i = 0; i < w.Count; i++)
        {
            double wi = w[i];
            if (wi < 0) throw new ArgumentException("Веса не могут быть отрицательными");
            total += wi;
        }
        if (total <= 0) throw new ArgumentException("Сумма весов равна нулю");

        Vector norm = new Vector(w.Count);
        for (int i = 0; i < w.Count; i++) norm[i] = w[i] / total;
        return norm;
    }

    private static bool DetermineOneD(IDistributionWithoutParams[] comps)
    {
        // Если хотя бы одна компонента отмечена ND — считаем смесь ND.
        for (int i = 0; i < comps.Length; i++)
            if (comps[i] is ISamplableDistribution s && !s.IsOneDimensional)
                return false;
        // Если компонент ничего не говорит о размерности — по дефолту 1D
        // (это совпадает с историческим поведением MixtureModel).
        return true;
    }

    private void EnsureOneD()
    {
        if (!_isOneD) throw new InvalidOperationException("Это многомерная смесь");
    }

    private void EnsureND()
    {
        if (_isOneD) throw new InvalidOperationException("Это одномерная смесь");
    }

    #endregion
}
