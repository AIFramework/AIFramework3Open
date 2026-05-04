using System;
using AI.ML.NeuralNetworks.V2.Autograd;

namespace AI.ML.NeuralNetworks.V2.Nn;

/// <summary>
/// Embedding-слой: индексная таблица обучаемых векторов.
/// </summary>
/// <remarks>
/// <para>
/// Аналог <c>torch.nn.Embedding</c>. Forward принимает индексный тензор формы
/// (...) и возвращает (..., embedding_dim). Backward аккумулирует градиент
/// в строки таблицы, на которые ссылались индексы.
/// </para>
/// <para>
/// Индексный тензор может быть <see cref="DType.Int32"/> или <see cref="DType.Int64"/>.
/// </para>
/// </remarks>
public sealed class Embedding : Module
{
    /// <summary>Размер словаря.</summary>
    public int NumEmbeddings { get; }

    /// <summary>Размер вектора эмбеддинга.</summary>
    public int EmbeddingDim { get; }

    /// <summary>Индекс паддинга (если задан, его эмбеддинг не обновляется и инициализирован 0).</summary>
    public int? PaddingIdx { get; }

    /// <summary>Веса (num_embeddings, embedding_dim).</summary>
    public Parameter Weight { get; }

    /// <summary>Создать Embedding-слой.</summary>
    public Embedding(int numEmbeddings, int embeddingDim, int? paddingIdx = null, Random rng = null)
    {
        if (numEmbeddings <= 0) throw new ArgumentOutOfRangeException(nameof(numEmbeddings));
        if (embeddingDim <= 0) throw new ArgumentOutOfRangeException(nameof(embeddingDim));
        NumEmbeddings = numEmbeddings;
        EmbeddingDim = embeddingDim;
        PaddingIdx = paddingIdx;

        var w = Tensor.Empty(new Shape(numEmbeddings, embeddingDim));
        Init.Normal_(w, 0f, 1f, rng);
        if (paddingIdx is int pad)
        {
            if (pad < 0 || pad >= numEmbeddings)
                throw new ArgumentOutOfRangeException(nameof(paddingIdx));
            var span = w.AsSpan<float>();
            int rowOff = pad * embeddingDim;
            for (int i = 0; i < embeddingDim; i++) span[rowOff + i] = 0f;
        }
        Weight = RegisterParameter("weight", w);
    }

    /// <inheritdoc/>
    public override Tensor Forward(Tensor input) => Lookup(Weight.Tensor, input, PaddingIdx);

    /// <summary>Функциональная форма (lookup).</summary>
    public static Tensor Lookup(Tensor weight, Tensor indices, int? paddingIdx = null)
    {
        if (weight.Rank != 2) throw new ArgumentException("weight должен быть 2D (num, dim).");

        // Сплющиваем индексы в 1D, потом вернём исходную форму + dim.
        int dim = weight.Shape[1];
        int numEmb = weight.Shape[0];
        long total = indices.NumElements;

        var outDims = new int[indices.Rank + 1];
        for (int i = 0; i < indices.Rank; i++) outDims[i] = indices.Shape[i];
        outDims[indices.Rank] = dim;
        var y = Tensor.Empty(new Shape(outDims), weight.DType, weight.Device);

        var ws = weight.Contiguous().AsReadOnlySpan<float>();
        var ys = y.AsSpan<float>();
        var idxC = indices.Contiguous();
        if (idxC.DType == DType.Int32)
        {
            var iSpan = idxC.AsReadOnlySpan<int>();
            for (long i = 0; i < total; i++)
            {
                int k = iSpan[(int)i];
                if (k < 0 || k >= numEmb)
                    throw new IndexOutOfRangeException(
                        $"Embedding: индекс {k} вне диапазона [0,{numEmb}).");
                int wOff = k * dim;
                int yOff = (int)i * dim;
                for (int d = 0; d < dim; d++) ys[yOff + d] = ws[wOff + d];
            }
        }
        else if (idxC.DType == DType.Int64)
        {
            var iSpan = idxC.AsReadOnlySpan<long>();
            for (long i = 0; i < total; i++)
            {
                int k = (int)iSpan[(int)i];
                if (k < 0 || k >= numEmb)
                    throw new IndexOutOfRangeException(
                        $"Embedding: индекс {k} вне диапазона [0,{numEmb}).");
                int wOff = k * dim;
                int yOff = (int)i * dim;
                for (int d = 0; d < dim; d++) ys[yOff + d] = ws[wOff + d];
            }
        }
        else
            throw new ArgumentException(
                $"Indices: ожидается Int32/Int64, фактически {idxC.DType}.");

        if (TapeContext.IsGradEnabled && weight.RequiresGrad)
        {
            var fn = new EmbeddingFunction(weight, idxC, paddingIdx);
            fn.RegisterInput(weight);
            y.GradFn = fn;
        }
        return y;
    }

    private sealed class EmbeddingFunction : Function
    {
        private readonly Tensor _w;
        private readonly Tensor _idx; // contiguous
        private readonly int? _padding;
        public EmbeddingFunction(Tensor w, Tensor idx, int? padding)
        {
            _w = w; _idx = idx; _padding = padding;
        }

        public override Tensor[] Backward(Tensor gradOutput)
        {
            int num = _w.Shape[0], dim = _w.Shape[1];
            var gW = Tensor.Zeros(_w.Shape, _w.DType, _w.Device);
            var gWs = gW.AsSpan<float>();
            var gys = gradOutput.Contiguous().AsReadOnlySpan<float>();
            long total = _idx.NumElements;

            if (_idx.DType == DType.Int32)
            {
                var iSpan = _idx.AsReadOnlySpan<int>();
                for (long i = 0; i < total; i++)
                {
                    int k = iSpan[(int)i];
                    if (_padding == k) continue;
                    int wOff = k * dim;
                    int yOff = (int)i * dim;
                    for (int d = 0; d < dim; d++) gWs[wOff + d] += gys[yOff + d];
                }
            }
            else
            {
                var iSpan = _idx.AsReadOnlySpan<long>();
                for (long i = 0; i < total; i++)
                {
                    int k = (int)iSpan[(int)i];
                    if (_padding == k) continue;
                    int wOff = k * dim;
                    int yOff = (int)i * dim;
                    for (int d = 0; d < dim; d++) gWs[wOff + d] += gys[yOff + d];
                }
            }
            return new[] { gW };
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"Embedding(num={NumEmbeddings}, dim={EmbeddingDim}, pad={PaddingIdx?.ToString() ?? "null"})";
}
