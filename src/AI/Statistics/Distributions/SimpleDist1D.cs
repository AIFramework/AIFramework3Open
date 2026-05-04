using AI.DataStructs.Algebraic;
using System;

namespace AI.Statistics.Distributions;

/// <summary>
/// Единый контракт для пере-оценки компонент по данным (MLE).
/// Используется в Classification EM для гетерогенных смесей.
/// 
/// Все компоненты реализуют оба метода; неподдерживаемый бросает
/// <see cref="NotSupportedException"/> (1D-компонента не знает о ND и наоборот).
/// Это позволяет использовать один и тот же алгоритм EM без дженериков.
/// </summary>
public interface IRefittable
{
    /// <summary>MLE-refit по 1D-данным. Возвращает новый экземпляр.</summary>
    IDistributionWithoutParams Refit1D(double[] data, int count);

    /// <summary>MLE-refit по ND-данным. Возвращает новый экземпляр.</summary>
    IDistributionWithoutParams RefitND(Vector[] data, int count);
}

// ═══════════════════════════════════════════════════════════════
//  Базовые классы
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Базовый класс для простых 1D-распределений.
/// Реализует <see cref="IDistributionWithoutParams"/>, <see cref="ISamplableDistribution"/>.
/// </summary>
public abstract class SimpleDist1DBase : IDistributionWithoutParams, ISamplableDistribution, IRefittable
{
    public abstract double CulcProb(double x);
    public double CulcLogProb(double x) => Math.Log(Math.Max(CulcProb(x), 1e-300));
    public double CulcProb(Vector x) => CulcProb(x[0]);
    public double CulcLogProb(Vector x) => CulcLogProb(x[0]);
    public abstract double Sample1D(Random rng);
    public Vector SampleND(Random rng) => new(Sample1D(rng));
    public bool IsOneDimensional => true;

    public abstract IDistributionWithoutParams Refit1D(double[] data, int count);
    public IDistributionWithoutParams RefitND(Vector[] data, int count)
        => throw new NotSupportedException("1D-компонента не поддерживает ND-refit");
}

/// <summary>
/// Базовый класс для ND-распределений с диагональной или полной ковариацией.
/// </summary>
public abstract class SimpleDistNDBase : IDistributionWithoutParams, ISamplableDistribution, IRefittable
{
    public abstract int Dim { get; }
    public double CulcProb(double x) => CulcProb(new Vector(x));
    public double CulcLogProb(double x) => CulcLogProb(new Vector(x));
    public abstract double CulcProb(Vector x);
    public abstract double CulcLogProb(Vector x);
    public double Sample1D(Random rng) => SampleND(rng)[0];
    public abstract Vector SampleND(Random rng);
    public bool IsOneDimensional => Dim == 1;

    public IDistributionWithoutParams Refit1D(double[] data, int count)
        => throw new NotSupportedException("ND-компонента не поддерживает 1D-refit");
    public abstract IDistributionWithoutParams RefitND(Vector[] data, int count);
}

// ═══════════════════════════════════════════════════════════════
//  1D-распределения
// ═══════════════════════════════════════════════════════════════

/// <summary>N(μ, σ²).</summary>
public sealed class GaussianDist1D : SimpleDist1DBase
{
    public double Mu { get; }
    public double Sigma { get; }

    public GaussianDist1D(double mu, double sigma) { Mu = mu; Sigma = Math.Max(sigma, 1e-6); }

    public override double CulcProb(double x)
    {
        double z = (x - Mu) / Sigma;
        return Math.Exp(-0.5 * z * z) / (Sigma * Math.Sqrt(2 * Math.PI));
    }

    public override double Sample1D(Random rng)
        => RandomEngine.NextGaussian(rng) * Sigma + Mu;

    public override IDistributionWithoutParams Refit1D(double[] data, int count)
    {
        double sum = 0;
        for (int i = 0; i < count; i++) sum += data[i];
        double mu = sum / count;
        double ss = 0;
        for (int i = 0; i < count; i++) { double d = data[i] - mu; ss += d * d; }
        return new GaussianDist1D(mu, Math.Sqrt(ss / Math.Max(count - 1, 1)));
    }
}

/// <summary>Exp(rate) со сдвигом.</summary>
public sealed class ExponentialDist1D : SimpleDist1DBase
{
    public double Rate { get; }
    public double Shift { get; }

    public ExponentialDist1D(double rate, double shift = 0) { Rate = Math.Max(rate, 1e-6); Shift = shift; }

    public override double CulcProb(double x)
    {
        double y = x - Shift;
        return y < 0 ? 0 : Rate * Math.Exp(-Rate * y);
    }

    public override double Sample1D(Random rng)
        => RandomEngine.NextExponential(rng, Rate) + Shift;

    public override IDistributionWithoutParams Refit1D(double[] data, int count)
    {
        double min = data[0];
        for (int i = 1; i < count; i++) if (data[i] < min) min = data[i];
        double sum = 0;
        for (int i = 0; i < count; i++) sum += data[i] - min;
        double mean = sum / count;
        return new ExponentialDist1D(mean > 1e-10 ? 1.0 / mean : 1.0, min);
    }
}

/// <summary>Laplace(μ, b).</summary>
public sealed class LaplaceDist1D : SimpleDist1DBase
{
    public double Mu { get; }
    public double B { get; }

    public LaplaceDist1D(double mu, double b) { Mu = mu; B = Math.Max(b, 1e-6); }

    public override double CulcProb(double x)
        => 0.5 / B * Math.Exp(-Math.Abs(x - Mu) / B);

    public override double Sample1D(Random rng)
        => RandomEngine.NextLaplace(rng, Mu, B);

    public override IDistributionWithoutParams Refit1D(double[] data, int count)
    {
        var sorted = new double[count];
        Array.Copy(data, sorted, count);
        Array.Sort(sorted);
        double mu = count % 2 == 0
            ? (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0
            : sorted[count / 2];
        double sumAbs = 0;
        for (int i = 0; i < count; i++) sumAbs += Math.Abs(data[i] - mu);
        return new LaplaceDist1D(mu, sumAbs / count);
    }
}

/// <summary>Rayleigh(σ).</summary>
public sealed class RayleighDist1D : SimpleDist1DBase
{
    public double Sigma { get; }

    public RayleighDist1D(double sigma) { Sigma = Math.Max(sigma, 1e-6); }

    public override double CulcProb(double x)
    {
        if (x <= 0) return 0;
        double s2 = Sigma * Sigma;
        return x / s2 * Math.Exp(-x * x / (2 * s2));
    }

    public override double Sample1D(Random rng)
        => RandomEngine.NextRayleigh(rng, Sigma);

    public override IDistributionWithoutParams Refit1D(double[] data, int count)
    {
        double ss = 0;
        for (int i = 0; i < count; i++) ss += data[i] * data[i];
        return new RayleighDist1D(Math.Sqrt(ss / (2.0 * count)));
    }
}

/// <summary>U(a, b).</summary>
public sealed class UniformDist1D : SimpleDist1DBase
{
    public double A { get; }
    public double B { get; }

    public UniformDist1D(double a, double b) { A = a; B = Math.Max(b, a + 1e-6); }

    public override double CulcProb(double x)
        => x >= A && x <= B ? 1.0 / (B - A) : 0;

    public override double Sample1D(Random rng)
        => A + rng.NextDouble() * (B - A);

    public override IDistributionWithoutParams Refit1D(double[] data, int count)
    {
        double min = data[0], max = data[0];
        for (int i = 1; i < count; i++)
        {
            if (data[i] < min) min = data[i];
            if (data[i] > max) max = data[i];
        }
        return new UniformDist1D(min, max);
    }
}

// ═══════════════════════════════════════════════════════════════
//  ND-распределения
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Многомерное гауссово распределение с диагональной ковариацией.
/// N(μ, diag(σ²)). Эффективно для случаев без корреляции между измерениями.
/// </summary>
public sealed class GaussianDistND : SimpleDistNDBase
{
    public Vector Mean { get; }
    public Vector Std { get; }
    public override int Dim => Mean.Count;

    public GaussianDistND(Vector mean, Vector std)
    {
        if (mean.Count != std.Count) throw new ArgumentException("dim mismatch");
        Mean = mean.Clone();
        Std = new Vector(std.Count);
        for (int d = 0; d < std.Count; d++)
            Std[d] = Math.Max(std[d], 1e-6);
    }

    public override double CulcLogProb(Vector x)
    {
        int dim = Dim;
        double logConst = 0.5 * dim * Math.Log(2.0 * Math.PI);
        double sumLogStd = 0, sumZ2 = 0;
        for (int d = 0; d < dim; d++)
        {
            sumLogStd += Math.Log(Std[d]);
            double z = (x[d] - Mean[d]) / Std[d];
            sumZ2 += z * z;
        }
        return -0.5 * sumZ2 - sumLogStd - logConst;
    }

    public override double CulcProb(Vector x) => Math.Exp(CulcLogProb(x));

    public override Vector SampleND(Random rng)
    {
        var v = new Vector(Dim);
        for (int d = 0; d < Dim; d++)
            v[d] = RandomEngine.NextGaussian(rng) * Std[d] + Mean[d];
        return v;
    }

    public override IDistributionWithoutParams RefitND(Vector[] data, int count)
    {
        int dim = Dim;
        var mean = new Vector(dim);
        for (int i = 0; i < count; i++)
            for (int d = 0; d < dim; d++)
                mean[d] += data[i][d];
        for (int d = 0; d < dim; d++) mean[d] /= count;

        var std = new Vector(dim);
        for (int i = 0; i < count; i++)
            for (int d = 0; d < dim; d++)
            {
                double diff = data[i][d] - mean[d];
                std[d] += diff * diff;
            }
        for (int d = 0; d < dim; d++)
            std[d] = Math.Sqrt(std[d] / Math.Max(count - 1, 1));

        return new GaussianDistND(mean, std);
    }
}

/// <summary>
/// Многомерное гауссово распределение с полной ковариационной матрицей.
/// N(μ, Σ). Позволяет моделировать корреляции между измерениями.
/// Использует разложение Холецкого для эффективного сэмплирования и плотности.
/// </summary>
public sealed class GaussianDistFullCov : SimpleDistNDBase
{
    public Vector Mean { get; }
    public override int Dim => Mean.Count;

    // Нижнетреугольная матрица Холецкого (L: Σ = L·Lᵀ)
    private readonly double[,] _chol;
    // log|Σ| = 2·Σ log(L_ii)
    private readonly double _logDet;
    // Обратная ковариационная (для logprob) — кэшируется
    private readonly double[,] _invCov;

    public GaussianDistFullCov(Vector mean, double[,] covariance)
    {
        int dim = mean.Count;
        if (covariance.GetLength(0) != dim || covariance.GetLength(1) != dim)
            throw new ArgumentException("cov matrix dim mismatch");
        Mean = mean.Clone();

        // Разложение Холецкого с регуляризацией диагонали
        _chol = new double[dim, dim];
        for (int i = 0; i < dim; i++)
            for (int j = 0; j < dim; j++)
                _chol[i, j] = covariance[i, j];

        // Малая добавка для числовой устойчивости
        for (int i = 0; i < dim; i++)
            _chol[i, i] += 1e-6;

        CholeskyInPlace(_chol, dim);

        _logDet = 0;
        for (int i = 0; i < dim; i++)
            _logDet += Math.Log(_chol[i, i]);
        _logDet *= 2;

        _invCov = InvertFromCholesky(_chol, dim);
    }

    public override double CulcLogProb(Vector x)
    {
        int dim = Dim;
        double logConst = 0.5 * dim * Math.Log(2.0 * Math.PI);
        double quad = 0;
        for (int i = 0; i < dim; i++)
            for (int j = 0; j < dim; j++)
                quad += (x[i] - Mean[i]) * _invCov[i, j] * (x[j] - Mean[j]);
        return -0.5 * quad - 0.5 * _logDet - logConst;
    }

    public override double CulcProb(Vector x) => Math.Exp(CulcLogProb(x));

    public override Vector SampleND(Random rng)
    {
        int dim = Dim;
        var z = new double[dim];
        for (int i = 0; i < dim; i++) z[i] = RandomEngine.NextGaussian(rng);

        var result = new Vector(dim);
        for (int i = 0; i < dim; i++)
        {
            double val = Mean[i];
            for (int j = 0; j <= i; j++)
                val += _chol[i, j] * z[j];
            result[i] = val;
        }
        return result;
    }

    public override IDistributionWithoutParams RefitND(Vector[] data, int count)
    {
        int dim = Dim;
        var mean = new Vector(dim);
        for (int i = 0; i < count; i++)
            for (int d = 0; d < dim; d++)
                mean[d] += data[i][d];
        for (int d = 0; d < dim; d++) mean[d] /= count;

        var cov = new double[dim, dim];
        for (int i = 0; i < count; i++)
            for (int r = 0; r < dim; r++)
                for (int c = r; c < dim; c++)
                {
                    double v = (data[i][r] - mean[r]) * (data[i][c] - mean[c]);
                    cov[r, c] += v;
                    if (r != c) cov[c, r] += v;
                }
        double denom = Math.Max(count - 1, 1);
        for (int r = 0; r < dim; r++)
            for (int c = 0; c < dim; c++)
                cov[r, c] /= denom;

        return new GaussianDistFullCov(mean, cov);
    }

    #region Линейная алгебра (Cholesky)

    /// <summary>In-place Cholesky L: A = L·Lᵀ. Нижнетреугольная часть перезаписывается.</summary>
    private static void CholeskyInPlace(double[,] a, int n)
    {
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = 0;
                for (int k = 0; k < j; k++) sum += a[i, k] * a[j, k];
                if (i == j)
                {
                    double diag = a[i, i] - sum;
                    a[i, j] = Math.Sqrt(Math.Max(diag, 1e-12));
                }
                else
                {
                    a[i, j] = (a[i, j] - sum) / a[j, j];
                }
            }
            // Обнуляем верхнюю часть
            for (int j = i + 1; j < n; j++) a[i, j] = 0;
        }
    }

    /// <summary>Вычисляет Σ⁻¹ из L (Cholesky): Σ⁻¹ = (L⁻¹)ᵀ · L⁻¹.</summary>
    private static double[,] InvertFromCholesky(double[,] L, int n)
    {
        // Обратная нижнетреугольная
        var Linv = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            Linv[i, i] = 1.0 / L[i, i];
            for (int j = i + 1; j < n; j++)
            {
                double sum = 0;
                for (int k = i; k < j; k++) sum += L[j, k] * Linv[k, i];
                Linv[j, i] = -sum / L[j, j];
            }
        }

        // Σ⁻¹ = Linv^T * Linv
        var inv = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = i; j < n; j++)
            {
                double sum = 0;
                for (int k = j; k < n; k++) sum += Linv[k, i] * Linv[k, j];
                inv[i, j] = sum;
                inv[j, i] = sum;
            }
        return inv;
    }

    #endregion
}
