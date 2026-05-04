using System;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Утилиты инициализации тензоров параметров (in-place).
/// </summary>
/// <remarks>
/// Аналог <c>torch.nn.init</c>. Все функции работают с Float32-тензором in-place
/// и возвращают тот же тензор для chaining.
/// </remarks>
public static class Init
{
    /// <summary>Заполнить нулями.</summary>
    public static Tensor Zeros_(Tensor t)
    {
        t.AsSpan<float>().Clear();
        return t;
    }

    /// <summary>Заполнить значением.</summary>
    public static Tensor Constant_(Tensor t, float value)
    {
        t.AsSpan<float>().Fill(value);
        return t;
    }

    /// <summary>Равномерное распределение [a, b).</summary>
    public static Tensor Uniform_(Tensor t, float a, float b, Random rng = null)
    {
        rng ??= Random.Shared;
        var span = t.AsSpan<float>();
        float w = b - a;
        for (int i = 0; i < span.Length; i++) span[i] = a + (float)rng.NextDouble() * w;
        return t;
    }

    /// <summary>Нормальное распределение N(mean, std).</summary>
    public static Tensor Normal_(Tensor t, float mean = 0f, float std = 1f, Random rng = null)
    {
        rng ??= Random.Shared;
        var span = t.AsSpan<float>();
        for (int i = 0; i < span.Length; i++)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            float z = (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
            span[i] = mean + std * z;
        }
        return t;
    }

    /// <summary>
    /// Xavier / Glorot uniform: U(-a, a), a = sqrt(6/(fan_in+fan_out)).
    /// </summary>
    public static Tensor XavierUniform_(Tensor t, float gain = 1f, Random rng = null, int groups = 1)
    {
        var (fanIn, fanOut) = CalcFanInOut(t, groups);
        float a = gain * MathF.Sqrt(6f / (fanIn + fanOut));
        return Uniform_(t, -a, a, rng);
    }

    /// <summary>Xavier / Glorot normal: N(0, std), std = gain * sqrt(2/(fan_in+fan_out)).</summary>
    public static Tensor XavierNormal_(Tensor t, float gain = 1f, Random rng = null, int groups = 1)
    {
        var (fanIn, fanOut) = CalcFanInOut(t, groups);
        float std = gain * MathF.Sqrt(2f / (fanIn + fanOut));
        return Normal_(t, 0f, std, rng);
    }

    /// <summary>
    /// Kaiming He uniform: U(-bound, bound), bound = gain * sqrt(3 / fan).
    /// По умолчанию <paramref name="mode"/> = "fan_in" (для ReLU-сетей).
    /// </summary>
    public static Tensor KaimingUniform_(Tensor t, float a = 0f, string mode = "fan_in",
        string nonlinearity = "leaky_relu", Random rng = null, int groups = 1)
    {
        var (fanIn, fanOut) = CalcFanInOut(t, groups);
        int fan = mode == "fan_out" ? fanOut : fanIn;
        float gain = CalculateGain(nonlinearity, a);
        float bound = gain * MathF.Sqrt(3f / fan);
        return Uniform_(t, -bound, bound, rng);
    }

    /// <summary>Kaiming He normal: N(0, std), std = gain / sqrt(fan).</summary>
    public static Tensor KaimingNormal_(Tensor t, float a = 0f, string mode = "fan_in",
        string nonlinearity = "leaky_relu", Random rng = null, int groups = 1)
    {
        var (fanIn, fanOut) = CalcFanInOut(t, groups);
        int fan = mode == "fan_out" ? fanOut : fanIn;
        float gain = CalculateGain(nonlinearity, a);
        float std = gain / MathF.Sqrt(fan);
        return Normal_(t, 0f, std, rng);
    }

    /// <summary>
    /// Подсчёт fan_in/fan_out для conv/linear-тензоров.
    /// </summary>
    /// <param name="t">Тензор веса. Conv: <c>(out_ch, in_ch/groups, kH, kW)</c>; Linear: <c>(out, in)</c>.</param>
    /// <param name="groups">Число групп для свёртки (1 — обычная свёртка).
    /// При <c>groups &gt; 1</c> учитывается уменьшение fan_out:
    /// <c>fan_out = out_ch / groups * k</c>.</param>
    public static (int fanIn, int fanOut) CalcFanInOut(Tensor t, int groups = 1)
    {
        if (t.Rank < 2)
            throw new ArgumentException("CalcFanInOut требует rank >= 2.");
        if (groups <= 0)
            throw new ArgumentOutOfRangeException(nameof(groups), "groups должен быть > 0.");
        int fanIn = t.Shape[1];          // для Conv это in_ch / groups (PyTorch-конвенция).
        int fanOut = t.Shape[0];
        if (t.Rank > 2)
        {
            int kernelMul = 1;
            for (int i = 2; i < t.Rank; i++) kernelMul *= t.Shape[i];
            fanIn *= kernelMul;
            // fan_out по PyTorch: out_ch / groups * k, т.к. каждая группа видит
            // только out_ch/groups выходных каналов.
            if (fanOut % groups != 0)
                throw new ArgumentException(
                    $"CalcFanInOut: fan_out({fanOut}) не делится на groups({groups}).");
            fanOut = (fanOut / groups) * kernelMul;
        }
        return (fanIn, fanOut);
    }

    /// <summary>Multipliers для Kaiming: gain зависит от nonlinearity.</summary>
    public static float CalculateGain(string nonlinearity, float param = 0f) => nonlinearity switch
    {
        "linear" or "conv1d" or "conv2d" or "conv3d" or "sigmoid" => 1f,
        "tanh" => 5f / 3f,
        "relu" => MathF.Sqrt(2f),
        "leaky_relu" => MathF.Sqrt(2f / (1f + param * param)),
        "selu" => 0.75f,
        _ => 1f
    };
}
