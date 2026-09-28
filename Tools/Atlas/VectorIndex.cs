using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Векторный канал: косинус между вектором запроса и векторами единиц полным перебором.
/// </summary>
/// <remarks>
/// При двенадцати тысячах единиц по 1024 измерения перебор занимает миллисекунды, и
/// приближённый индекс (Faiss, HNSW) дал бы только лишнюю зависимость. Векторы хранятся
/// нормированными, поэтому косинус — скалярное произведение.
/// </remarks>
public sealed class VectorIndex
{
    private const int Batch = 256;
    private const int MaxTextLength = 2000;

    private readonly IEmbedderService _embedder;
    private readonly float[][] _vectors;

    private VectorIndex(IEmbedderService embedder, float[][] vectors)
    {
        _embedder = embedder;
        _vectors = vectors;
    }

    /// <summary>
    /// Векторизует единицы пачками: при кэширующем эмбеддере сбой посреди прогона не теряет
    /// уже полученного, и повторный запуск продолжает с места обрыва.
    /// </summary>
    public static async Task<VectorIndex> BuildAsync(
        IReadOnlyList<CodeUnit> units, IEmbedderService embedder, IProgress<int>? progress = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(embedder);

        var vectors = new float[units.Count][];

        for (int start = 0; start < units.Count; start += Batch)
        {
            int count = Math.Min(Batch, units.Count - start);
            Vector[] batch = await embedder.EncodeAsync(units.Skip(start).Take(count).Select(Text), cancellation);

            for (int i = 0; i < count; i++) vectors[start + i] = Normalize(batch[i]);

            progress?.Report(start + count);
        }

        return new VectorIndex(embedder, vectors);
    }

    /// <summary>Текст единицы для векторизации: имя, сигнатура, описание.</summary>
    public static string Text(CodeUnit unit)
    {
        string text = $"{ApiSearch.NameText(unit)}\n{unit.Signature}\n{ApiSearch.DocText(unit.Doc)}".Trim();
        return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
    }

    /// <summary>Ближайшие к запросу единицы, по убыванию косинуса.</summary>
    public async Task<(int Index, double Score)[]> SearchAsync(string query, int limit, CancellationToken cancellation = default)
    {
        float[] q = Normalize(await _embedder.EncodeQuestionAsync(query, cancellation));
        var scores = new (int Index, double Score)[_vectors.Length];

        for (int i = 0; i < _vectors.Length; i++)
        {
            float[] v = _vectors[i];
            double dot = 0;
            for (int d = 0; d < v.Length; d++) dot += v[d] * q[d];
            scores[i] = (i, dot);
        }

        return [.. scores.OrderByDescending(score => score.Score).ThenBy(score => score.Index).Take(limit)];
    }

    private static float[] Normalize(Vector vector)
    {
        double norm = Math.Sqrt(vector.Sum(value => value * value));
        return [.. vector.Select(value => (float)(norm > 0 ? value / norm : 0))];
    }
}
