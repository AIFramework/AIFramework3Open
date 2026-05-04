using System;
using AI.ML.NeuralNetworks.V2.Ops;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Inverted Dropout: в train-режиме случайно зануляет долю элементов и
/// делит остальные на (1-p), чтобы матожидание сохранилось. В eval-режиме —
/// тождество.
/// </summary>
/// <remarks>
/// <para>
/// Маска генерируется через <see cref="Random"/> переданный в конструктор
/// (или <see cref="Random.Shared"/> по умолчанию — потокобезопасно).
/// Backward проходит через стандартный Mul: маска не требует градиента,
/// поэтому autograd корректно даёт <c>gx = gy * mask / (1-p)</c>.
/// </para>
/// </remarks>
public sealed class Dropout : Module
{
    /// <summary>Вероятность занулить элемент.</summary>
    public float P { get; }

    private readonly Random _rng;

    /// <summary>Создать Dropout c вероятностью <paramref name="p"/>.</summary>
    public Dropout(float p = 0.5f, Random rng = null)
    {
        if (p < 0f || p >= 1f)
            throw new ArgumentOutOfRangeException(nameof(p), "p ∈ [0, 1).");
        P = p;
        _rng = rng;
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input)
    {
        if (!Training || P == 0f) return input;
        var rng = _rng ?? Random.Shared;

        // 1) Если backend зарегистрировал GPU/устройство-нативный Dropout,
        //    используем его — маска генерируется без CPU-detour'а.
        var kernel = OpRegistry.TryGet(OpCode.Dropout, input.DType, input.Device);
        if (kernel != null)
        {
            int seed;
            lock (rng) seed = rng.Next();
            var attrs = new DropoutAttrs(P, seed);
            var outs = kernel(new[] { input }, attrs);
            if (outs == null || outs.Length == 0)
                throw new InvalidOperationException("Dropout-kernel вернул пустой результат.");
            return outs[0];
        }

        // 2) Универсальный путь: маска на CPU + копирование на устройство (если нужно).
        //    Создаётся как leaf-тензор без grad, чтобы не попасть в граф autograd.
        var mask = Tensor.Empty(input.Shape, input.DType, Device.Cpu);
        var ms = mask.AsSpan<float>();
        float keep = 1f - P;
        float invKeep = 1f / keep;
        lock (rng)
        {
            for (int i = 0; i < ms.Length; i++)
                ms[i] = rng.NextDouble() < keep ? invKeep : 0f;
        }
        if (input.Device.Type != DeviceType.Cpu) mask = mask.To(input.Device);
        return input * mask;
    }

    /// <summary>Атрибуты для backend-Dropout-kernel-а.</summary>
    public readonly struct DropoutAttrs
    {
        /// <summary>Вероятность занулить элемент.</summary>
        public float P { get; }
        /// <summary>Seed для генерации маски (детерминизм).</summary>
        public int Seed { get; }
        /// <summary>Создать атрибуты.</summary>
        public DropoutAttrs(float p, int seed) { P = p; Seed = seed; }
    }

    /// <inheritdoc/>
    public override string ToString() => $"Dropout(p={P})";
}
