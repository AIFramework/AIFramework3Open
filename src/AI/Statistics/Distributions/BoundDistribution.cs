using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.Statistics.Distributions;

/// <summary>
/// «Связывает» параметризованное распределение (<see cref="IDistribution"/>)
/// с конкретными параметрами и превращает его в бескомпонентное
/// (<see cref="IDistributionWithoutParams"/>).
/// 
/// Это позволяет единообразно собирать смеси из каких угодно
/// компонент: 1D-гауссиан, ND-гауссиан, пользовательских
/// распределений — и делать рекурсивные композиции.
/// </summary>
[Serializable]
public sealed class BoundDistribution1D : IDistributionWithoutParams, ISamplableDistribution
{
    private readonly IDistribution _inner;
    private readonly Dictionary<string, double> _params;
    private readonly NonCorrelatedGaussian _samplerAsGaussian;

    /// <summary>Связывает 1D-распределение с параметрами.</summary>
    public BoundDistribution1D(IDistribution inner, Dictionary<string, double> parameters)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _samplerAsGaussian = inner as NonCorrelatedGaussian;
    }

    /// <summary>Параметры, с которыми связано распределение.</summary>
    public IReadOnlyDictionary<string, double> Parameters => _params;

    /// <inheritdoc/>
    public double CulcProb(double x) => _inner.CulcProb(x, _params);
    /// <inheritdoc/>
    public double CulcLogProb(double x) => _inner.CulcLogProb(x, _params);

    /// <inheritdoc/>
    public double CulcProb(Vector x)
        => throw new NotSupportedException("Это одномерное распределение");
    /// <inheritdoc/>
    public double CulcLogProb(Vector x)
        => throw new NotSupportedException("Это одномерное распределение");

    /// <inheritdoc/>
    public bool IsOneDimensional => true;

    /// <inheritdoc/>
    public double Sample1D(Random rng)
    {
        if (_samplerAsGaussian != null)
            return _samplerAsGaussian.Sample(_params, rng);
        throw new NotSupportedException(
            $"Распределение {_inner.GetType().Name} не поддерживает прямое сэмплирование. " +
            "Используйте MCMC_1D для произвольной плотности.");
    }

    /// <inheritdoc/>
    public Vector SampleND(Random rng)
        => throw new NotSupportedException("Это одномерное распределение");
}

/// <summary>
/// ND-версия <see cref="BoundDistribution1D"/>.
/// </summary>
[Serializable]
public sealed class BoundDistributionND : IDistributionWithoutParams, ISamplableDistribution
{
    private readonly IDistribution _inner;
    private readonly Dictionary<string, Vector> _params;
    private readonly NonCorrelatedGaussian _samplerAsGaussian;

    /// <summary>Связывает ND-распределение с параметрами.</summary>
    public BoundDistributionND(IDistribution inner, Dictionary<string, Vector> parameters)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _samplerAsGaussian = inner as NonCorrelatedGaussian;
    }

    /// <summary>Параметры, с которыми связано распределение.</summary>
    public IReadOnlyDictionary<string, Vector> Parameters => _params;

    /// <inheritdoc/>
    public double CulcProb(Vector x) => _inner.CulcProb(x, _params);
    /// <inheritdoc/>
    public double CulcLogProb(Vector x) => _inner.CulcLogProb(x, _params);

    /// <inheritdoc/>
    public double CulcProb(double x)
        => throw new NotSupportedException("Это многомерное распределение");
    /// <inheritdoc/>
    public double CulcLogProb(double x)
        => throw new NotSupportedException("Это многомерное распределение");

    /// <inheritdoc/>
    public bool IsOneDimensional => false;

    /// <inheritdoc/>
    public double Sample1D(Random rng)
        => throw new NotSupportedException("Это многомерное распределение");

    /// <inheritdoc/>
    public Vector SampleND(Random rng)
    {
        if (_samplerAsGaussian != null)
            return _samplerAsGaussian.Sample(_params, rng);
        throw new NotSupportedException(
            $"Распределение {_inner.GetType().Name} не поддерживает прямое сэмплирование.");
    }
}
