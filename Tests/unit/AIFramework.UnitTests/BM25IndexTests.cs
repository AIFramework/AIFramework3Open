using AI.NLP;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Поиск BM25 по обратному индексу обязан совпадать с полным перебором <see cref="BM25.Score(string, int)"/>
/// до бита: и оценки, и порядок, включая равенства и дополнение нулями.
/// </summary>
public class BM25IndexTests
{
    private static readonly string[] Corpus =
    [
        "Фильтр Калмана оценивает состояние линейной системы по зашумлённым измерениям",
        "Быстрое преобразование Фурье вычисляет спектр сигнала",
        "Спектр мощности сигнала и оконные функции",
        "Кластеризация методом k-средних",
        "Фильтр нижних частот сглаживает сигнал",
        "Оценка спектральной плотности методом Уэлча",
        "Собственные значения симметричной матрицы",
        "Фильтр Калмана для нелинейных систем: расширенный и сигма-точечный",
    ];

    [Theory]
    [InlineData("фильтр калмана", 5)]
    [InlineData("спектр сигнала", 8)]
    [InlineData("спектр спектр сигнала", 3)]
    [InlineData("слово которого нет", 4)]
    [InlineData("матрицы", 20)]
    public void IndexedSearchMatchesFullScan(string query, int n)
    {
        var bm = new BM25(Corpus);

        (int index, double score)[] expected = Enumerable.Range(0, Corpus.Length)
            .Select(i => (i, bm.Score(query, i)))
            .OrderByDescending(x => x.Item2)
            .Take(Math.Min(n, Corpus.Length))
            .ToArray();

        Assert.Equal(expected, bm.SearchTopN(query, n));
    }

    [Fact]
    public void SearchReturnsBestOrFirstDocument()
    {
        var bm = new BM25(Corpus);

        Assert.Equal(6, bm.Search("собственные значения"));
        Assert.Equal(0, bm.Search("ничего похожего"));
    }

    [Fact]
    public void PreTokenizedCorpusUsesTokensAsIs()
    {
        IReadOnlyList<IReadOnlyList<string>> docs =
        [
            ["kalman", "filter", "state"],
            ["fft", "spectrum"],
            ["kalman", "kalman", "smoother"],
        ];

        var bm = new BM25(docs);
        (int index, double score)[] top = bm.SearchTopN(["kalman"], 3);

        Assert.Equal([2, 0, 1], top.Select(x => x.index));
        Assert.Equal(0.0, top[2].score);
        Assert.Equal(2, bm.DocumentFrequency("kalman"));
        Assert.Equal(bm.Score(["kalman"], 0), top[1].score);
    }

    [Fact]
    public void EmptyCorpusIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new BM25(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => new BM25(Array.Empty<IReadOnlyList<string>>()));
    }
}
