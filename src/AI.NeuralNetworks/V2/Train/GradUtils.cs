using System;
using System.Collections.Generic;
using AI.ML.NeuralNetworks.V2.Nn;

namespace AI.ML.NeuralNetworks.V2.Train;

/// <summary>
/// Утилиты для работы с градиентами (clip, norm).
/// </summary>
public static class GradUtils
{
    /// <summary>Глобальная L2-норма всех градиентов.</summary>
    public static float TotalGradNorm(IEnumerable<Parameter> parameters)
    {
        double sumSq = 0;
        foreach (var p in parameters)
        {
            var g = p.Tensor.Grad;
            if (g == null) continue;
            var s = g.Contiguous().AsReadOnlySpan<float>();
            for (int i = 0; i < s.Length; i++) sumSq += s[i] * s[i];
        }
        return (float)Math.Sqrt(sumSq);
    }

    /// <summary>
    /// Глобальный clip по L2-норме: если ‖g‖ > maxNorm, масштабирует все градиенты
    /// на коэффициент maxNorm / ‖g‖. Возвращает фактическую исходную норму.
    /// </summary>
    /// <remarks>
    /// In-place масштабирование требует <c>Contiguous()</c> над <c>g</c>, иначе
    /// AsSpan на view-тензоре с stride!=row-major даст ошибочную картинку памяти.
    /// </remarks>
    public static float ClipGradNorm(IEnumerable<Parameter> parameters, float maxNorm)
    {
        float total = TotalGradNorm(parameters);
        if (total <= maxNorm || total == 0f) return total;
        float scale = maxNorm / (total + 1e-6f);
        foreach (var p in parameters)
        {
            var g = p.Tensor.Grad;
            if (g == null) continue;
            // Если grad — view (например, после reshape/permute), AsSpan вернёт
            // непрерывную область памяти storage'а, что не совпадает с логическим
            // порядком элементов. Делаем grad contiguous in-place, копируя данные
            // обратно в Tensor.Grad.
            if (!g.IsContiguous)
            {
                var c = g.Contiguous();
                p.Tensor.Grad = c;
                g = c;
            }
            var s = g.AsSpan<float>();
            for (int i = 0; i < s.Length; i++) s[i] *= scale;
        }
        return total;
    }

    /// <summary>Поэлементный clip градиентов.</summary>
    public static void ClipGradValue(IEnumerable<Parameter> parameters, float clipValue)
    {
        foreach (var p in parameters)
        {
            var g = p.Tensor.Grad;
            if (g == null) continue;
            if (!g.IsContiguous)
            {
                var c = g.Contiguous();
                p.Tensor.Grad = c;
                g = c;
            }
            var s = g.AsSpan<float>();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] > clipValue) s[i] = clipValue;
                else if (s[i] < -clipValue) s[i] = -clipValue;
            }
        }
    }
}

/// <summary>
/// Exponential Moving Average параметров (Polyak averaging).
/// </summary>
/// <remarks>
/// Полезно для стабилизации test-метрик: <c>EMA[θ] <- decay·EMA[θ] + (1-decay)·θ</c>.
/// </remarks>
public sealed class ParameterEMA
{
    private readonly Dictionary<Parameter, Tensor> _shadow = new();
    /// <summary>Коэффициент decay (близкий к 1).</summary>
    public float Decay { get; }
    /// <summary>Создать.</summary>
    public ParameterEMA(IEnumerable<Parameter> parameters, float decay = 0.999f)
    {
        if (decay <= 0 || decay >= 1)
            throw new ArgumentOutOfRangeException(nameof(decay));
        Decay = decay;
        foreach (var p in parameters)
        {
            var copy = Tensor.Empty(p.Tensor.Shape, p.Tensor.DType, p.Tensor.Device);
            p.Tensor.Contiguous().AsReadOnlySpan<float>().CopyTo(copy.AsSpan<float>());
            _shadow[p] = copy;
        }
    }
    /// <summary>Обновить EMA после optimizer.Step().</summary>
    public void Update()
    {
        foreach (var (p, shadow) in _shadow)
        {
            var s = shadow.AsSpan<float>();
            var t = p.Tensor.Contiguous().AsReadOnlySpan<float>();
            for (int i = 0; i < s.Length; i++)
                s[i] = Decay * s[i] + (1f - Decay) * t[i];
        }
    }
    /// <summary>Скопировать EMA-веса в реальные параметры (например, перед eval).</summary>
    public void CopyTo()
    {
        foreach (var (p, shadow) in _shadow)
            shadow.AsReadOnlySpan<float>().CopyTo(p.Tensor.AsSpan<float>());
    }
    /// <summary>Получить EMA-копию для параметра.</summary>
    public Tensor Get(Parameter p) => _shadow[p];
}
