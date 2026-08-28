using AI.NLP;
using AI.Script.Semantics;
using AI.Script.Runtime;

namespace AI.Script.Std;

/// <summary>
/// Поисковый индекс по корпусу: TF-IDF либо BM25 за одним интерфейсом.
/// </summary>
/// <remarks>
/// Обёртка нужна по двум причинам. Во-первых, <c>TFIDF</c> из фреймворка не хранит размер
/// корпуса публично, а без него нельзя ранжировать выдачу — приходится помнить его самим.
/// Во-вторых, две модели с разными сигнатурами превратились бы в две ветки в каждой функции
/// модуля; здесь ветка одна и она здесь.
/// </remarks>
public sealed class TextIndex
{
    private readonly TFIDF? _tfidf;
    private readonly BM25? _bm25;

    /// <summary>Вид индекса.</summary>
    public string Kind { get; }

    /// <summary>Число документов в корпусе.</summary>
    public int Count { get; }

    /// <summary>Создаёт индекс TF-IDF.</summary>
    public TextIndex(TFIDF model, int count)
    {
        _tfidf = model;
        Kind = "TF-IDF";
        Count = count;
    }

    /// <summary>Создаёт индекс BM25.</summary>
    public TextIndex(BM25 model, int count)
    {
        _bm25 = model;
        Kind = "BM25";
        Count = count;
    }

    /// <summary>Оценка соответствия документа запросу.</summary>
    public double Score(string query, int document)
    {
        if (document < 0 || document >= Count)
        {
            throw new ScriptError(
                DiagnosticCodes.IndexOutOfRange,
                $"номер документа {document} вне границ: в корпусе {Count}");
        }

        return _bm25 != null ? _bm25.Score(query, document) : _tfidf!.TF_IDF_Str(query, document);
    }

    /// <summary>Лучшие документы по запросу, от самого подходящего.</summary>
    public IReadOnlyList<(int Document, double Score)> Search(string query, int top)
    {
        var scored = new List<(int Document, double Score)>(Count);

        for (int i = 0; i < Count; i++) scored.Add((i, Score(query, i)));

        scored.Sort((left, right) => right.Score.CompareTo(left.Score));

        return scored.GetRange(0, Math.Min(top, scored.Count));
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Kind}, документов: {Count}";
}
