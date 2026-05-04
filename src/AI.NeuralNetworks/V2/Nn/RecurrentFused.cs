using System;
using System.Runtime.CompilerServices;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Fused CPU-операции для одного шага LSTM/GRU/RNN-ячейки. Сворачивают
/// 8–12 поэлементных tensor-ops в один линейный проход по памяти, благодаря
/// чему RNN-обучение на CPU становится в разы быстрее: исключаются
/// промежуточные allocation/Function-узлы и Contiguous-копии non-contig
/// view-ов после <see cref="Ops.IndexingOps.Narrow"/>.
/// </summary>
/// <remarks>
/// <para>
/// Все методы работают только для <see cref="DType.Float32"/> на CPU.
/// Для не-CPU устройств вызывающий код должен использовать обычный
/// composed-путь (через <see cref="Ops.TensorOps"/>).
/// </para>
/// <para>
/// <b>Autograd:</b> каждый fused-step регистрирует один <see cref="Function"/>
/// в графе вместо ~10 узлов. Backward проходит за один линейный sweep
/// (для LSTM сохраняются <c>i/f/g/o/tanh(c)/c_prev</c>; для GRU —
/// <c>r/z/n/h_prev</c>).
/// </para>
/// </remarks>
internal static class RecurrentFused
{
    /// <summary>
    /// Один шаг LSTM. <paramref name="preact"/> = (B, 4H) — pre-activation gates
    /// в порядке (i, f, g, o); <paramref name="cPrev"/> = (B, H).
    /// Возвращает packed-тензор формы (2, B, H): [0] = h_new, [1] = c_new.
    /// </summary>
    /// <remarks>
    /// Распаковка: <c>Select(packed, 0, 0)</c> и <c>Select(packed, 0, 1)</c> —
    /// оба contiguous (B, H) view-а.
    /// </remarks>
    public static Tensor LstmStep(Tensor preact, Tensor cPrev)
    {
        if (preact == null) throw new ArgumentNullException(nameof(preact));
        if (cPrev == null) throw new ArgumentNullException(nameof(cPrev));
        if (preact.DType != DType.Float32 || cPrev.DType != DType.Float32)
            throw new ArgumentException("LstmStep: только Float32.");
        if (preact.Device.Type != DeviceType.Cpu || cPrev.Device.Type != DeviceType.Cpu)
            throw new ArgumentException("LstmStep: только CPU (для GPU используйте composed-путь).");
        if (preact.Rank != 2 || cPrev.Rank != 2)
            throw new ArgumentException($"LstmStep: ожидаются 2D, получено preact={preact.Shape}, cPrev={cPrev.Shape}.");
        int B = preact.Shape[0];
        int H4 = preact.Shape[1];
        if ((H4 & 3) != 0) throw new ArgumentException($"LstmStep: preact.Shape[1] должно делиться на 4, получено {H4}.");
        int H = H4 / 4;
        if (cPrev.Shape[0] != B || cPrev.Shape[1] != H)
            throw new ArgumentException($"LstmStep: cPrev должен быть ({B},{H}), получено {cPrev.Shape}.");

        var preactC = preact.IsContiguous ? preact : preact.Contiguous();
        var cPrevC = cPrev.IsContiguous ? cPrev : cPrev.Contiguous();

        var packed = Tensor.Empty(new Shape(2, B, H));

        // Сохранённые активации для backward — одна аллокация на 5*B*H float
        // (i, f, g, o, tanh(c)) последовательными планами длины B*H.
        int planeBH = B * H;
        bool needGrad = TapeContext.IsGradEnabled && (preact.RequiresGrad || cPrev.RequiresGrad);
        var saved = needGrad ? new float[5 * planeBH] : null;

        var preSpan = preactC.AsReadOnlySpan<float>();
        var cPrevSpan = cPrevC.AsReadOnlySpan<float>();
        var packedSpan = packed.AsSpan<float>();

        // packed layout: [hPlane | cPlane] (2 * B*H).
        Span<float> hOut = packedSpan.Slice(0, planeBH);
        Span<float> cOut = packedSpan.Slice(planeBH, planeBH);
        Span<float> savedSpan = needGrad ? saved.AsSpan() : default;

        for (int b = 0; b < B; b++)
        {
            int preBase = b * H4;
            int outBase = b * H;
            int iOff = preBase;
            int fOff = preBase + H;
            int gOff = preBase + 2 * H;
            int oOff = preBase + 3 * H;
            for (int hi = 0; hi < H; hi++)
            {
                float gi = Sigmoid(preSpan[iOff + hi]);
                float gf = Sigmoid(preSpan[fOff + hi]);
                float gg = MathF.Tanh(preSpan[gOff + hi]);
                float go = Sigmoid(preSpan[oOff + hi]);
                float cNew = gf * cPrevSpan[outBase + hi] + gi * gg;
                float tanhC = MathF.Tanh(cNew);
                float hNew = go * tanhC;

                int sIdx = outBase + hi;
                if (needGrad)
                {
                    savedSpan[sIdx] = gi;
                    savedSpan[planeBH + sIdx] = gf;
                    savedSpan[2 * planeBH + sIdx] = gg;
                    savedSpan[3 * planeBH + sIdx] = go;
                    savedSpan[4 * planeBH + sIdx] = tanhC;
                }
                hOut[sIdx] = hNew;
                cOut[sIdx] = cNew;
            }
        }

        if (needGrad)
        {
            var fn = new LstmStepFn(B, H, saved, cPrevC);
            fn.RegisterInput(preact);
            fn.RegisterInput(cPrev);
            packed.GradFn = fn;
        }
        return packed;
    }

    /// <summary>
    /// Один шаг GRU. <paramref name="gx"/> = (B, 3H) и <paramref name="gh"/> = (B, 3H) —
    /// уже посчитанные <c>x@W_ih^T (+b_ih)</c> и <c>h@W_hh^T (+b_hh)</c> с порядком
    /// гейтов (r, z, n). <paramref name="hPrev"/> = (B, H). Возвращает h_new (B, H).
    /// </summary>
    public static Tensor GruStep(Tensor gx, Tensor gh, Tensor hPrev)
    {
        if (gx == null) throw new ArgumentNullException(nameof(gx));
        if (gh == null) throw new ArgumentNullException(nameof(gh));
        if (hPrev == null) throw new ArgumentNullException(nameof(hPrev));
        if (gx.DType != DType.Float32 || gh.DType != DType.Float32 || hPrev.DType != DType.Float32)
            throw new ArgumentException("GruStep: только Float32.");
        if (gx.Device.Type != DeviceType.Cpu || gh.Device.Type != DeviceType.Cpu || hPrev.Device.Type != DeviceType.Cpu)
            throw new ArgumentException("GruStep: только CPU.");
        if (gx.Rank != 2 || gh.Rank != 2 || hPrev.Rank != 2)
            throw new ArgumentException("GruStep: все входы должны быть 2D.");
        int B = gx.Shape[0];
        int H3 = gx.Shape[1];
        if ((H3 % 3) != 0) throw new ArgumentException($"GruStep: gx.Shape[1] должно делиться на 3, получено {H3}.");
        int H = H3 / 3;
        if (gh.Shape[0] != B || gh.Shape[1] != H3)
            throw new ArgumentException($"GruStep: gh должен быть ({B},{H3}), получено {gh.Shape}.");
        if (hPrev.Shape[0] != B || hPrev.Shape[1] != H)
            throw new ArgumentException($"GruStep: hPrev должен быть ({B},{H}), получено {hPrev.Shape}.");

        var gxC = gx.IsContiguous ? gx : gx.Contiguous();
        var ghC = gh.IsContiguous ? gh : gh.Contiguous();
        var hPrevC = hPrev.IsContiguous ? hPrev : hPrev.Contiguous();

        var output = Tensor.Empty(new Shape(B, H));

        // Сохранённые значения для backward — одна аллокация (4*B*H float):
        // r, z, n, nh последовательными планами длины B*H.
        int planeBH = B * H;
        bool needGrad = TapeContext.IsGradEnabled && (gx.RequiresGrad || gh.RequiresGrad || hPrev.RequiresGrad);
        var saved = needGrad ? new float[4 * planeBH] : null;

        var gxSpan = gxC.AsReadOnlySpan<float>();
        var ghSpan = ghC.AsReadOnlySpan<float>();
        var hPrevSpan = hPrevC.AsReadOnlySpan<float>();
        var outSpan = output.AsSpan<float>();
        Span<float> savedSpan = needGrad ? saved.AsSpan() : default;

        for (int b = 0; b < B; b++)
        {
            int gxBase = b * H3;
            int ghBase = b * H3;
            int hBase = b * H;
            int rxOff = gxBase;
            int zxOff = gxBase + H;
            int nxOff = gxBase + 2 * H;
            int rhOff = ghBase;
            int zhOff = ghBase + H;
            int nhOff = ghBase + 2 * H;

            for (int hi = 0; hi < H; hi++)
            {
                float r = Sigmoid(gxSpan[rxOff + hi] + ghSpan[rhOff + hi]);
                float z = Sigmoid(gxSpan[zxOff + hi] + ghSpan[zhOff + hi]);
                float nh = ghSpan[nhOff + hi];
                float n = MathF.Tanh(gxSpan[nxOff + hi] + r * nh);
                float hp = hPrevSpan[hBase + hi];
                float hNew = (1f - z) * n + z * hp;

                int sIdx = hBase + hi;
                if (needGrad)
                {
                    savedSpan[sIdx] = r;
                    savedSpan[planeBH + sIdx] = z;
                    savedSpan[2 * planeBH + sIdx] = n;
                    savedSpan[3 * planeBH + sIdx] = nh;
                }
                outSpan[sIdx] = hNew;
            }
        }

        if (needGrad)
        {
            var fn = new GruStepFn(B, H, saved, hPrevC);
            fn.RegisterInput(gx);
            fn.RegisterInput(gh);
            fn.RegisterInput(hPrev);
            output.GradFn = fn;
        }
        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Sigmoid(float x)
    {
        // 1 / (1 + exp(-x)) с защитой от overflow.
        if (x >= 0f) { float e = MathF.Exp(-x); return 1f / (1f + e); }
        else { float e = MathF.Exp(x); return e / (1f + e); }
    }

    private sealed class LstmStepFn : Function
    {
        private readonly int _B, _H;
        private readonly float[] _saved; // 5 * B*H: i, f, g, o, tanh(c)
        private readonly Tensor _cPrev;

        public LstmStepFn(int B, int H, float[] saved, Tensor cPrev)
        {
            _B = B; _H = H; _saved = saved; _cPrev = cPrev;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            // gradOutput shape: (2, B, H), [0]=dh, [1]=dc.
            if (gradOutput.Rank != 3 || gradOutput.Shape[0] != 2 ||
                gradOutput.Shape[1] != _B || gradOutput.Shape[2] != _H)
                throw new InvalidOperationException(
                    $"LstmStepFn: неверная форма gradOutput {gradOutput.Shape}, ожидалось (2,{_B},{_H}).");

            var gOutC = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
            var gOutSpan = gOutC.AsReadOnlySpan<float>();
            int planeBH = _B * _H;
            ReadOnlySpan<float> dh = gOutSpan.Slice(0, planeBH);
            ReadOnlySpan<float> dc = gOutSpan.Slice(planeBH, planeBH);

            int H4 = 4 * _H;
            var dPre = Tensor.Empty(new Shape(_B, H4));
            var dCPrev = Tensor.Empty(new Shape(_B, _H));
            var dPreSpan = dPre.AsSpan<float>();
            var dCPrevSpan = dCPrev.AsSpan<float>();
            var cPrevC = _cPrev.IsContiguous ? _cPrev : _cPrev.Contiguous();
            var cPrevSpan = cPrevC.AsReadOnlySpan<float>();
            ReadOnlySpan<float> savedSpan = _saved;
            ReadOnlySpan<float> sI = savedSpan.Slice(0, planeBH);
            ReadOnlySpan<float> sF = savedSpan.Slice(planeBH, planeBH);
            ReadOnlySpan<float> sG = savedSpan.Slice(2 * planeBH, planeBH);
            ReadOnlySpan<float> sO = savedSpan.Slice(3 * planeBH, planeBH);
            ReadOnlySpan<float> sTC = savedSpan.Slice(4 * planeBH, planeBH);

            for (int b = 0; b < _B; b++)
            {
                int preBase = b * H4;
                int hBase = b * _H;
                int iOff = preBase;
                int fOff = preBase + _H;
                int gOff = preBase + 2 * _H;
                int oOff = preBase + 3 * _H;
                for (int hi = 0; hi < _H; hi++)
                {
                    int sIdx = hBase + hi;
                    float gi = sI[sIdx];
                    float gf = sF[sIdx];
                    float gg = sG[sIdx];
                    float go = sO[sIdx];
                    float tanhC = sTC[sIdx];
                    float cPrev = cPrevSpan[sIdx];

                    float dhi = dh[sIdx];
                    float dci = dc[sIdx];

                    // c_new = gf*c_prev + gi*gg; h_new = go * tanh(c_new).
                    float dGo = dhi * tanhC;
                    float dTanhC = dhi * go;
                    float dC = dci + dTanhC * (1f - tanhC * tanhC);

                    float dGi = dC * gg;
                    float dGg = dC * gi;
                    float dGf = dC * cPrev;
                    float dCPrevElem = dC * gf;

                    // pre-activation gradients (через производные sigmoid/tanh).
                    dPreSpan[iOff + hi] = dGi * gi * (1f - gi);
                    dPreSpan[fOff + hi] = dGf * gf * (1f - gf);
                    dPreSpan[gOff + hi] = dGg * (1f - gg * gg);
                    dPreSpan[oOff + hi] = dGo * go * (1f - go);
                    dCPrevSpan[sIdx] = dCPrevElem;
                }
            }
            return new[] { dPre, dCPrev };
        }
    }

    private sealed class GruStepFn : Function
    {
        private readonly int _B, _H;
        private readonly float[] _saved; // 4 * B*H: r, z, n, nh
        private readonly Tensor _hPrev;

        public GruStepFn(int B, int H, float[] saved, Tensor hPrev)
        {
            _B = B; _H = H; _saved = saved; _hPrev = hPrev;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            if (gradOutput.Rank != 2 || gradOutput.Shape[0] != _B || gradOutput.Shape[1] != _H)
                throw new InvalidOperationException(
                    $"GruStepFn: неверная форма gradOutput {gradOutput.Shape}, ожидалось ({_B},{_H}).");

            var gOutC = gradOutput.IsContiguous ? gradOutput : gradOutput.Contiguous();
            var dh = gOutC.AsReadOnlySpan<float>();
            var hPrevC = _hPrev.IsContiguous ? _hPrev : _hPrev.Contiguous();
            var hPrevSpan = hPrevC.AsReadOnlySpan<float>();

            int H3 = 3 * _H;
            int planeBH = _B * _H;
            var dGx = Tensor.Empty(new Shape(_B, H3));
            var dGh = Tensor.Empty(new Shape(_B, H3));
            var dHPrev = Tensor.Empty(new Shape(_B, _H));
            var dGxSpan = dGx.AsSpan<float>();
            var dGhSpan = dGh.AsSpan<float>();
            var dHPrevSpan = dHPrev.AsSpan<float>();
            ReadOnlySpan<float> savedSpan = _saved;
            ReadOnlySpan<float> sR = savedSpan.Slice(0, planeBH);
            ReadOnlySpan<float> sZ = savedSpan.Slice(planeBH, planeBH);
            ReadOnlySpan<float> sN = savedSpan.Slice(2 * planeBH, planeBH);
            ReadOnlySpan<float> sNh = savedSpan.Slice(3 * planeBH, planeBH);

            for (int b = 0; b < _B; b++)
            {
                int gBase = b * H3;
                int hBase = b * _H;
                int rxOff = gBase;
                int zxOff = gBase + _H;
                int nxOff = gBase + 2 * _H;
                int rhOff = gBase;
                int zhOff = gBase + _H;
                int nhOff = gBase + 2 * _H;
                for (int hi = 0; hi < _H; hi++)
                {
                    int sIdx = hBase + hi;
                    float r = sR[sIdx];
                    float z = sZ[sIdx];
                    float n = sN[sIdx];
                    float nh = sNh[sIdx];
                    float hp = hPrevSpan[sIdx];
                    float dhi = dh[sIdx];

                    // h_new = (1-z)*n + z*h_prev.
                    float dN = dhi * (1f - z);
                    float dZ = dhi * (hp - n);
                    float dHp = dhi * z;

                    // n = tanh(nx + r*nh).
                    float dPreN = dN * (1f - n * n);
                    float dNx = dPreN;
                    float dR = dPreN * nh;
                    float dNh = dPreN * r;

                    float dPreR = dR * r * (1f - r);
                    float dPreZ = dZ * z * (1f - z);

                    dGxSpan[rxOff + hi] = dPreR;
                    dGxSpan[zxOff + hi] = dPreZ;
                    dGxSpan[nxOff + hi] = dNx;

                    dGhSpan[rhOff + hi] = dPreR;
                    dGhSpan[zhOff + hi] = dPreZ;
                    dGhSpan[nhOff + hi] = dNh;

                    dHPrevSpan[sIdx] = dHp;
                }
            }
            return new[] { dGx, dGh, dHPrev };
        }
    }
}
