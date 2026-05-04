using ILGPU;
using ILGPU.Algorithms;

namespace AI.ML.NeuralNetworks.Gpu.V2;

public static partial class V2Kernels
{
    #region Strided->contiguous копия (rank ≤ 6)
    // Используется для Tensor.Contiguous() на GPU без D2H/H2D round-trip.
    // Выходной тензор всегда row-major-contiguous; для входного тензора
    // передаётся (offset, strides, shape) до 6 осей.

    /// <summary>
    /// dst[i] = src[srcOffset + sum_k i_k · ss_k], где i раскладывается по dims O0..O5
    /// (row-major, последняя ось — самая быстрая). Ось с dim=1 и stride=0 — паддинговая.
    /// </summary>
    public static void ContiguousCopy6D(Index1D i,
        ArrayView<float> src, ArrayView<float> dst,
        StridedCopyArgs args)
    {
        long lin = i;
        int i5 = (int)(lin % args.O5); lin /= args.O5;
        int i4 = (int)(lin % args.O4); lin /= args.O4;
        int i3 = (int)(lin % args.O3); lin /= args.O3;
        int i2 = (int)(lin % args.O2); lin /= args.O2;
        int i1 = (int)(lin % args.O1); lin /= args.O1;
        int i0 = (int)lin;
        int srcIdx = args.SrcOffset
                     + i0 * args.SS0 + i1 * args.SS1 + i2 * args.SS2
                     + i3 * args.SS3 + i4 * args.SS4 + i5 * args.SS5;
        dst[i] = src[srcIdx];
    }

    /// <summary>
    /// Обратный scatter: <c>dst[dstOffset + sum_k i_k · ss_k] = src[i]</c>.
    /// Источник contiguous (i — линейный индекс), приёмник — strided view.
    /// Поля <see cref="StridedCopyArgs"/> переинтерпретируются: <c>SrcOffset</c> — это
    /// offset в DST, а <c>SS*</c> — strides DST.
    /// </summary>
    /// <remarks>
    /// Используется для:
    /// <list type="bullet">
    /// <item>GPU-Cat: scatter каждого входа в нужный slice выходного тензора.</item>
    /// <item>NarrowFunction.Backward / SelectFunction.Backward: scatter градиента
    /// в zero-padded grad ОЗ исходной формы (на GPU, без D2H/H2D).</item>
    /// </list>
    /// </remarks>
    public static void ScatterContiguous6D(Index1D i,
        ArrayView<float> src, ArrayView<float> dst,
        StridedCopyArgs args)
    {
        long lin = i;
        int i5 = (int)(lin % args.O5); lin /= args.O5;
        int i4 = (int)(lin % args.O4); lin /= args.O4;
        int i3 = (int)(lin % args.O3); lin /= args.O3;
        int i2 = (int)(lin % args.O2); lin /= args.O2;
        int i1 = (int)(lin % args.O1); lin /= args.O1;
        int i0 = (int)lin;
        int dstIdx = args.SrcOffset
                     + i0 * args.SS0 + i1 * args.SS1 + i2 * args.SS2
                     + i3 * args.SS3 + i4 * args.SS4 + i5 * args.SS5;
        dst[dstIdx] = src[i];
    }

    #endregion Strided->contiguous копия (rank ≤ 6)

    #region Fused LSTM/GRU step (forward + backward)
    // Один kernel-launch обрабатывает один (b, hi) элемент, что эквивалентно
    // ~10 element-wise ops в CPU-композиции, и устраняет per-step overhead
    // от 4 Sigmoid/Tanh/Mul/Add kernel-launches и 4 Narrow/Function-узлов.
    //
    // Layout preact = (B, 4H) в порядке (i, f, g, o) — те же offsets, что и в
    // CPU-варианте RecurrentFused.LstmStep.
    //
    // saved[5*B*H]: i, f, g, o, tanh(c) последовательными планами (длиной B*H каждый).

    /// <summary>
    /// Fused LSTM step forward: обрабатывает один (b, hi) элемент в потоке.
    /// Параметр <paramref name="needSave"/> = 1, если нужно сохранить активации
    /// в <paramref name="saved"/> для backward (иначе — пропускаем запись).
    /// </summary>
    public static void LstmStepFwd(Index1D i,
        ArrayView<float> preact, ArrayView<float> cPrev,
        ArrayView<float> hOut, ArrayView<float> cOut,
        ArrayView<float> saved,
        int H, int planeBH, int needSave)
    {
        int b = (int)(i / H);
        int hi = (int)(i % H);
        int H4 = 4 * H;
        int preBase = b * H4;
        int sIdx = b * H + hi;

        float xI = preact[preBase + 0 * H + hi];
        float xF = preact[preBase + 1 * H + hi];
        float xG = preact[preBase + 2 * H + hi];
        float xO = preact[preBase + 3 * H + hi];

        float gi = StableSigmoid(xI);
        float gf = StableSigmoid(xF);
        float gg = XMath.Tanh(xG);
        float go = StableSigmoid(xO);

        float cP = cPrev[sIdx];
        float cNew = gf * cP + gi * gg;
        float tanhC = XMath.Tanh(cNew);
        float hNew = go * tanhC;

        hOut[sIdx] = hNew;
        cOut[sIdx] = cNew;

        if (needSave != 0)
        {
            saved[0 * planeBH + sIdx] = gi;
            saved[1 * planeBH + sIdx] = gf;
            saved[2 * planeBH + sIdx] = gg;
            saved[3 * planeBH + sIdx] = go;
            saved[4 * planeBH + sIdx] = tanhC;
        }
    }

    /// <summary>
    /// Fused LSTM step backward: даёт <c>dPre (B, 4H)</c> и <c>dCPrev (B, H)</c>
    /// напрямую из сохранённых активаций. Один поток на (b, hi).
    /// </summary>
    public static void LstmStepBwd(Index1D i,
        ArrayView<float> dh, ArrayView<float> dc,
        ArrayView<float> saved, ArrayView<float> cPrev,
        ArrayView<float> dPre, ArrayView<float> dCPrev,
        int H, int planeBH)
    {
        int b = (int)(i / H);
        int hi = (int)(i % H);
        int H4 = 4 * H;
        int preBase = b * H4;
        int sIdx = b * H + hi;

        float gi = saved[0 * planeBH + sIdx];
        float gf = saved[1 * planeBH + sIdx];
        float gg = saved[2 * planeBH + sIdx];
        float go = saved[3 * planeBH + sIdx];
        float tanhC = saved[4 * planeBH + sIdx];
        float cP = cPrev[sIdx];

        float dhi = dh[sIdx];
        float dci = dc[sIdx];

        float dGo = dhi * tanhC;
        float dTanhC = dhi * go;
        float dC = dci + dTanhC * (1f - tanhC * tanhC);

        float dGi = dC * gg;
        float dGg = dC * gi;
        float dGf = dC * cP;
        float dCp = dC * gf;

        dPre[preBase + 0 * H + hi] = dGi * gi * (1f - gi);
        dPre[preBase + 1 * H + hi] = dGf * gf * (1f - gf);
        dPre[preBase + 2 * H + hi] = dGg * (1f - gg * gg);
        dPre[preBase + 3 * H + hi] = dGo * go * (1f - go);
        dCPrev[sIdx] = dCp;
    }

    /// <summary>
    /// Fused GRU step forward: один поток на (b, hi). Layout gx/gh = (B, 3H)
    /// в порядке (r, z, n). Сохраняет r/z/n/nh для backward.
    /// </summary>
    public static void GruStepFwd(Index1D i,
        ArrayView<float> gx, ArrayView<float> gh, ArrayView<float> hPrev,
        ArrayView<float> hOut, ArrayView<float> saved,
        int H, int planeBH, int needSave)
    {
        int b = (int)(i / H);
        int hi = (int)(i % H);
        int H3 = 3 * H;
        int gBase = b * H3;
        int sIdx = b * H + hi;

        float rx = gx[gBase + 0 * H + hi];
        float zx = gx[gBase + 1 * H + hi];
        float nx = gx[gBase + 2 * H + hi];
        float rh = gh[gBase + 0 * H + hi];
        float zh = gh[gBase + 1 * H + hi];
        float nh = gh[gBase + 2 * H + hi];

        float r = StableSigmoid(rx + rh);
        float z = StableSigmoid(zx + zh);
        float n = XMath.Tanh(nx + r * nh);
        float hp = hPrev[sIdx];
        float hNew = (1f - z) * n + z * hp;

        hOut[sIdx] = hNew;

        if (needSave != 0)
        {
            saved[0 * planeBH + sIdx] = r;
            saved[1 * planeBH + sIdx] = z;
            saved[2 * planeBH + sIdx] = n;
            saved[3 * planeBH + sIdx] = nh;
        }
    }

    /// <summary>
    /// Fused GRU step backward: один поток на (b, hi). Даёт dGx/dGh/dHPrev
    /// напрямую из сохранённых активаций.
    /// </summary>
    public static void GruStepBwd(Index1D i,
        ArrayView<float> dh, ArrayView<float> saved, ArrayView<float> hPrev,
        ArrayView<float> dGx, ArrayView<float> dGh, ArrayView<float> dHPrev,
        int H, int planeBH)
    {
        int b = (int)(i / H);
        int hi = (int)(i % H);
        int H3 = 3 * H;
        int gBase = b * H3;
        int sIdx = b * H + hi;

        float r = saved[0 * planeBH + sIdx];
        float z = saved[1 * planeBH + sIdx];
        float n = saved[2 * planeBH + sIdx];
        float nh = saved[3 * planeBH + sIdx];
        float hp = hPrev[sIdx];
        float dhi = dh[sIdx];

        float dN = dhi * (1f - z);
        float dZ = dhi * (hp - n);
        float dHp = dhi * z;

        float dPreN = dN * (1f - n * n);
        float dNx = dPreN;
        float dR = dPreN * nh;
        float dNh = dPreN * r;

        float dPreR = dR * r * (1f - r);
        float dPreZ = dZ * z * (1f - z);

        dGx[gBase + 0 * H + hi] = dPreR;
        dGx[gBase + 1 * H + hi] = dPreZ;
        dGx[gBase + 2 * H + hi] = dNx;

        dGh[gBase + 0 * H + hi] = dPreR;
        dGh[gBase + 1 * H + hi] = dPreZ;
        dGh[gBase + 2 * H + hi] = dNh;

        dHPrev[sIdx] = dHp;
    }
    #endregion Fused LSTM/GRU step (forward + backward)
}
