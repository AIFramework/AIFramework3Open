using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Llm;

/// <summary>
/// Общие операции над векторными представлениями.
/// </summary>
/// <remarks>
/// Косинусная близость считается здесь, а не берётся из библиотеки: эмбеддеры возвращают
/// векторы разной нормировки, и предположение «они уже нормированы» — самая частая причина
/// того, что поиск «работает, но выдаёт не то».
/// </remarks>
internal static class Embeddings
{
    /// <summary>Складывает векторы в матрицу «текст × признак».</summary>
    public static Matrix ToMatrix(IReadOnlyList<Vector> vectors, IScriptContext context)
    {
        if (vectors.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "эмбеддер не вернул ни одного вектора");

        int width = vectors[0].Count;

        for (int i = 1; i < vectors.Count; i++)
        {
            if (vectors[i].Count == width) continue;

            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"эмбеддер вернул векторы разной длины: {width} и {vectors[i].Count}");
        }

        context.CountAllocation((long)vectors.Count * width);

        var matrix = new Matrix(vectors.Count, width);

        for (int i = 0; i < vectors.Count; i++)
        {
            for (int j = 0; j < width; j++) matrix[i, j] = vectors[i][j];
        }

        return matrix;
    }

    /// <summary>Косинусная близость двух векторов; ноль, если один из них нулевой.</summary>
    public static double Cosine(Vector a, Vector b)
    {
        if (a.Count != b.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"длины векторов различаются: {a.Count} и {b.Count}");
        }

        return AI.Distances.BaseDist.CosineSimilarity(a, b, whenUndefined: 0);
    }
}
